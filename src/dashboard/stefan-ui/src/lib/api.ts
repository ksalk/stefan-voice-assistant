import type {
	Command,
	CommandsResult,
	GetCommandLogsResult,
	GetCommandToolsResult,
	GetNodeDetailsResult,
	GetNodesResult,
	ToolDocumentDetails,
	ToolDocumentsResult
} from './types';

const normalizeApiOrigin = (url: string) =>
	url
		.trim()
		.replace(/\/+$/, '')
		.replace(/\/api$/, '');

const resolveBaseUrl = (): string => {
	const configuredUrl = import.meta.env.VITE_API_URL;
	if (!configuredUrl) {
		throw new Error(
			'VITE_API_URL is required but was not set. Define it in a local .env file or through the active mise environment.'
		);
	}

	return `${normalizeApiOrigin(configuredUrl)}/api`;
};

const BASE_URL = resolveBaseUrl();

async function send({
	method,
	path,
	data,
	token,
	fetch: customFetch
}: {
	method: string;
	path: string;
	data?: unknown;
	token?: string;
	fetch?: typeof fetch;
}) {
	const opts: RequestInit = { method, headers: new Headers() };

	if (data) {
		(opts.headers as Headers).set('Content-Type', 'application/json');
		opts.body = JSON.stringify(data);
	}

	if (token) {
		(opts.headers as Headers).set('Authorization', `Bearer ${token}`);
	}

	const f = customFetch || fetch;
	const res = await f(`${BASE_URL}/${path}`, opts);

	console.log(`API request: ${method} ${BASE_URL}/${path}`, { method, path, data, token });
	if (!res.ok) {
		console.error(`API error: ${res.status} ${res.statusText}`);
		const errorData = await res.json().catch(() => ({}));
		throw new Error(errorData.message || 'Something went wrong');
	}

	return res.status === 204 ? {} : res.json();
}

export const api = {
	getNodes: (customFetch?: typeof fetch): Promise<GetNodesResult> =>
		send({ method: 'GET', path: 'nodes', fetch: customFetch }),

	getNode: (id: string, customFetch?: typeof fetch): Promise<GetNodeDetailsResult> =>
		send({ method: 'GET', path: `nodes/${id}`, fetch: customFetch }),

	pingNode: (id: string, customFetch?: typeof fetch) =>
		send({ method: 'POST', path: `nodes/${id}/ping`, fetch: customFetch }),

	speakText: (nodeId: string, text: string, customFetch?: typeof fetch) =>
		send({
			method: 'POST',
			path: `nodes/${nodeId}/speak-text`,
			data: { text },
			fetch: customFetch
		}),

	getCommands: (
		page: number,
		pageSize: number,
		options: { nodeId?: string; statuses?: string[] } = {},
		customFetch?: typeof fetch
	): Promise<CommandsResult> => {
		const params = new URLSearchParams();
		params.set('page', String(page));
		params.set('pageSize', String(pageSize));
		if (options.nodeId) {
			params.set('nodeId', options.nodeId);
		}
		for (const status of options.statuses ?? []) {
			params.append('status', status);
		}
		return send({ method: 'GET', path: `commands?${params.toString()}`, fetch: customFetch });
	},

	getCommand: (id: string, customFetch?: typeof fetch): Promise<Command> =>
		send({ method: 'GET', path: `commands/${id}`, fetch: customFetch }),

	getCommandTools: (id: string, customFetch?: typeof fetch): Promise<GetCommandToolsResult> =>
		send({ method: 'GET', path: `commands/${id}/tools`, fetch: customFetch }),

	getCommandLogs: (id: string, customFetch?: typeof fetch): Promise<GetCommandLogsResult> =>
		send({ method: 'GET', path: `commands/${id}/logs`, fetch: customFetch }),

	getToolDocuments: (
		page: number,
		pageSize: number,
		options: {
			scope?: string;
			type?: string;
			search?: string;
			sortBy?: string;
			sortDirection?: string;
		} = {},
		customFetch?: typeof fetch
	): Promise<ToolDocumentsResult> => {
		const params = new URLSearchParams();
		params.set('page', String(page));
		params.set('pageSize', String(pageSize));
		if (options.scope) params.set('scope', options.scope);
		if (options.type) params.set('type', options.type);
		if (options.search) params.set('search', options.search);
		if (options.sortBy) params.set('sortBy', options.sortBy);
		if (options.sortDirection) params.set('sortDirection', options.sortDirection);
		return send({ method: 'GET', path: `tool-documents?${params.toString()}`, fetch: customFetch });
	},

	getToolDocument: (
		id: string,
		customFetch?: typeof fetch
	): Promise<{ document: ToolDocumentDetails }> =>
		send({ method: 'GET', path: `tool-documents/${id}`, fetch: customFetch }),

	getCommandAudio: async (commandId: string, type: 'Request' | 'Response') => {
		const res = await fetch(`${BASE_URL}/commands/${commandId}/audio?type=${type}`);
		if (!res.ok) {
			const errorData = await res.json().catch(() => ({}));
			throw new Error(errorData.message || 'Failed to load audio');
		}
		return res.blob();
	}
};
