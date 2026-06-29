<script lang="ts">
	import './layout.css';
	import favicon from '$lib/assets/favicon.svg';
	import Sidebar from '$lib/components/Sidebar.svelte';
	import { Toaster } from '$lib/components/ui/sonner';

	let { children } = $props();
</script>

<svelte:head><link rel="icon" href={favicon} /></svelte:head>

<div class="flex min-h-screen">
	<Sidebar />
	<main class="ml-64 flex-1 bg-slate-50 p-6 text-slate-900">
		{@render children()}
	</main>
</div>

<Toaster richColors />
