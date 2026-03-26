import { api } from '$lib/api';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ fetch }) => {
    // Pass the special SvelteKit fetch into your service
    const nodes = await api.getNodes(fetch);

    return nodes;
};