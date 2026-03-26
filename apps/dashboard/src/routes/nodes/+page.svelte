<script lang="ts">
    import type { PageProps } from './$types';

	let { data }: PageProps = $props();
</script>

<h1>Nodes</h1>

{#if data.nodes.length === 0}
	<p class="text-slate-400">No nodes found. Please add a node to get started.</p>
{:else}
	<ul class="mt-4 space-y-2">
		{#each data.nodes as node (node.id)}
			<li class="rounded-lg bg-slate-300 p-4">
				<h2 class="text-lg font-semibold">{node.name}</h2>
				<p class="text-sm text-slate-400">{node.lastKnownIpAddress}</p>
			</li>
		{/each}
	</ul>
{/if}
