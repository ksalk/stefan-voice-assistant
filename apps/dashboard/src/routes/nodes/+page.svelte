<script lang="ts">
	import { onMount } from 'svelte';
	import * as Table from '$lib/components/ui/table';
	import { Button } from '$lib/components/ui/button/index.js';
	import CirclePlay from '@lucide/svelte/icons/circle-play';
	import TimeAgo from '$lib/components/TimeAgo.svelte';
	import { formatDateTime } from '$lib/utils/date';
	import { api } from '$lib/api';

	let nodes: any[] = $state([]);
	let loading = $state(true);
	let error: string | null = $state(null);

	onMount(async () => {
		await fetchNodes();
	});

	async function fetchNodes() {
		try {
			const response = await api.getNodes();
			nodes = response.nodes || [];
		} catch (e) {
			error = e instanceof Error ? e.message : 'Failed to load nodes';
		} finally {
			loading = false;
		}
	}

	async function pingNode(id: string) {
		await api.pingNode(id);
		await fetchNodes();
	}
</script>

<h1>Nodes</h1>

{#if loading}
	<p class="mt-4 text-slate-600">Loading nodes...</p>
{:else if error}
	<div class="mt-4 rounded border border-red-200 bg-red-50 p-4 text-red-700">
		<p>Error loading nodes: {error}</p>
	</div>
{:else}
	<Table.Root class="mt-4">
		<Table.Caption>Nodes registered in the system.</Table.Caption>
		<Table.Header>
			<Table.Row>
				<Table.Head class="w-[100px]">Name</Table.Head>
				<Table.Head>Status</Table.Head>
				<Table.Head>IP</Table.Head>
				<Table.Head>Registered</Table.Head>
				<Table.Head>Last Ping</Table.Head>
				<Table.Head>Ping Now</Table.Head>
			</Table.Row>
		</Table.Header>
		<Table.Body>
			{#each nodes as node (node.id)}
				<Table.Row>
					<Table.Cell class="font-medium">{node.name}</Table.Cell>
					<Table.Cell>{node.status}</Table.Cell>
					<Table.Cell>{node.lastKnownIpAddress}:{node.port}</Table.Cell>
					<Table.Cell>{formatDateTime(node.registeredAt)}</Table.Cell>
					<Table.Cell><TimeAgo date={node.lastPingAt} /></Table.Cell>
					<Table.Cell>
						<Button
							variant="outline"
							size="icon"
							aria-label="Ping node"
							onclick={() => pingNode(node.id)}
						>
							<CirclePlay />
						</Button>
					</Table.Cell>
				</Table.Row>
			{/each}
		</Table.Body>
	</Table.Root>
{/if}
