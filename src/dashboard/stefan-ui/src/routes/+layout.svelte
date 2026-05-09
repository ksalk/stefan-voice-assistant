<script lang="ts">
	import './layout.css';
	import favicon from '$lib/assets/favicon.svg';
	import Sidebar from '$lib/components/Sidebar.svelte';

	let { children } = $props();
</script>

<svelte:head><link rel="icon" href={favicon} /></svelte:head>

<div class="flex min-h-screen">
	<Sidebar />
	<main class="ml-64 flex-1 p-6 bg-slate-50 text-slate-900">
		{@render children()}
	</main>
</div>
