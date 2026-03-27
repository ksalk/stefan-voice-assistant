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
	data?: any;
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
	getNodes: (customFetch?: typeof fetch) =>
		send({ method: 'GET', path: 'nodes', fetch: customFetch }),

	pingNode: (id: string, customFetch?: typeof fetch) =>
		send({ method: 'POST', path: `nodes/${id}/ping`, fetch: customFetch })

	// getPost: (id: string) => send({ method: 'GET', path: `posts/${id}` }),

	// createPost: (data: { title: string; content: string }) =>
	//     send({ method: 'POST', path: 'posts', data }),

	// deletePost: (id: string) => send({ method: 'DELETE', path: `posts/${id}` })
};
