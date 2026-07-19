import type { Command, CommandsResult, GetNodeDetailsResult, GetNodesResult } from './types';

const BASE_URL = 'http://localhost:5285/api';

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

	getCommandAudio: async (commandId: string, type: 'Request' | 'Response') => {
		const res = await fetch(`${BASE_URL}/commands/${commandId}/audio?type=${type}`);
		if (!res.ok) {
			const errorData = await res.json().catch(() => ({}));
			throw new Error(errorData.message || 'Failed to load audio');
		}
		return res.blob();
	}
};
