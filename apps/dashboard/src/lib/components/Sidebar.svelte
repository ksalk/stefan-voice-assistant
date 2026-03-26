<script lang="ts">
	/* eslint-disable svelte/no-navigation-without-resolve */
	import { page } from '$app/state';

	const menuItems = [
		{
			name: 'Dashboard',
			href: '/',
			icon: `<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-4 0a1 1 0 01-1-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 01-1 1h-2z"/>`
		},
		{
			name: 'Commands',
			href: '/commands',
			icon: `<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/>`
		},
		{
			name: 'Nodes',
			href: '/nodes',
			icon: `<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01"/>`
		}
	];

	function isActive(href: string): boolean {
		if (href === '/') return page.url.pathname === '/';
		return page.url.pathname.startsWith(href);
	}
</script>

<aside class="fixed inset-y-0 left-0 w-64 bg-slate-900 text-slate-300">
	<div class="flex h-16 items-center px-6">
		<span class="text-lg font-bold text-white">Admin</span>
	</div>

	<nav class="mt-4 px-3">
		{#each menuItems as item (item.name)}
			<a
				href={item.href}
				class="mb-1 flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors
					{isActive(item.href)
					? 'bg-slate-800 text-white'
					: 'text-slate-400 hover:bg-slate-800 hover:text-white'}"
			>
				<svg class="h-5 w-5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
					<!-- eslint-disable-next-line svelte/no-at-html-tags -->
					{@html item.icon}
				</svg>
				{item.name}
			</a>
		{/each}
	</nav>
</aside>
