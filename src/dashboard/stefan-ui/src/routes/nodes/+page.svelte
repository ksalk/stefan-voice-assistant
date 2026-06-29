<script lang="ts">
	import { onMount } from 'svelte';
	import * as Table from '$lib/components/ui/table';
	import * as Alert from '$lib/components/ui/alert/index.js';
	import * as ToggleGroup from '$lib/components/ui/toggle-group/index.js';
	import { Button } from '$lib/components/ui/button/index.js';
	import { Badge } from '$lib/components/ui/badge/index.js';
	import { Skeleton } from '$lib/components/ui/skeleton/index.js';
	import RefreshCw from '@lucide/svelte/icons/refresh-cw';
	import Radio from '@lucide/svelte/icons/radio';
	import TimeAgo from '$lib/components/TimeAgo.svelte';
	import { api } from '$lib/api';
	import type { NodeSummary } from '$lib/types';
	import { toast } from 'svelte-sonner';

	type Filter = 'all' | 'active';

	let nodes: NodeSummary[] = $state([]);
	let loading = $state(true);
	let refreshing = $state(false);
	let pingingId: string | null = $state(null);
	let error: string | null = $state(null);
	let filter = $state<Filter>('active');

	const onlineNodes = $derived(nodes.filter((n) => n.status === 'Online'));
	const offlineNodes = $derived(nodes.filter((n) => n.status === 'Offline'));
	const filteredNodes = $derived(filter === 'active' ? onlineNodes : nodes);
	const counts = $derived({
		online: onlineNodes.length,
		offline: offlineNodes.length,
		total: nodes.length
	});

	onMount(async () => {
		await fetchNodes();
	});

	async function fetchNodes() {
		loading = nodes.length === 0;
		refreshing = nodes.length > 0;
		error = null;
		try {
			const response = await api.getNodes();
			nodes = response.nodes ?? [];
		} catch (e) {
			error = e instanceof Error ? e.message : 'Failed to load nodes';
		} finally {
			loading = false;
			refreshing = false;
		}
	}

	async function pingNode(id: string) {
		if (pingingId) return;
		pingingId = id;
		const node = nodes.find((n) => n.id === id);
		const name = node?.name ?? id;
		try {
			await api.pingNode(id);
			toast.success(`Pinged ${name}`);
			try {
				await fetchNodes();
			} catch (e) {
				const msg = e instanceof Error ? e.message : 'Unknown error';
				toast.error(`Failed to refresh ${name}: ${msg}`);
			}
		} catch (e) {
			const msg = e instanceof Error ? e.message : 'Unknown error';
			toast.error(`Failed to ping ${name}: ${msg}`);
		} finally {
			pingingId = null;
		}
	}

	function emptyMessage(): string {
		return filter === 'active' ? 'No active nodes.' : 'No nodes registered.';
	}
</script>

<h1>Nodes</h1>

<div class="mt-4 flex flex-wrap items-center justify-between gap-3">
	<div class="flex items-center gap-3">
		<ToggleGroup.Root type="single" bind:value={filter} variant="outline">
			<ToggleGroup.Item value="all" aria-label="Show all nodes">All</ToggleGroup.Item>
			<ToggleGroup.Item value="active" aria-label="Show active nodes only">Active</ToggleGroup.Item>
		</ToggleGroup.Root>
		<span class="text-sm text-muted-foreground">
			{#if loading}
				…
			{:else}
				{counts.online} online · {counts.offline} offline · {counts.total} total
			{/if}
		</span>
	</div>
	<Button variant="outline" size="sm" onclick={() => fetchNodes()} disabled={loading || refreshing}>
		<RefreshCw class="size-4" />
		Refresh
	</Button>
</div>

{#if loading}
	<Table.Root class="mt-4">
		<Table.Header>
			<Table.Row>
				<Table.Head>Name</Table.Head>
				<Table.Head class="w-[90px]">Status</Table.Head>
				<Table.Head>Address</Table.Head>
				<Table.Head class="w-[110px]">Last Seen</Table.Head>
				<Table.Head class="w-[110px]">Last Ping</Table.Head>
				<Table.Head class="w-[80px]">Restarts</Table.Head>
				<Table.Head class="w-[70px]">Actions</Table.Head>
			</Table.Row>
		</Table.Header>
		<Table.Body>
			{#each [0, 1, 2, 3, 4] as i (i)}
				<Table.Row>
					<Table.Cell><Skeleton class="h-4 w-32" /></Table.Cell>
					<Table.Cell><Skeleton class="h-5 w-16 rounded-full" /></Table.Cell>
					<Table.Cell><Skeleton class="h-4 w-28" /></Table.Cell>
					<Table.Cell><Skeleton class="h-4 w-16" /></Table.Cell>
					<Table.Cell><Skeleton class="h-4 w-16" /></Table.Cell>
					<Table.Cell><Skeleton class="h-4 w-8" /></Table.Cell>
					<Table.Cell><Skeleton class="h-8 w-8 rounded-md" /></Table.Cell>
				</Table.Row>
			{/each}
		</Table.Body>
	</Table.Root>
{:else if error}
	<Alert.Root variant="destructive" class="mt-4">
		<Alert.Title>Error</Alert.Title>
		<Alert.Description>{error}</Alert.Description>
	</Alert.Root>
{:else}
	<Table.Root class="mt-4">
		<Table.Header>
			<Table.Row>
				<Table.Head>Name</Table.Head>
				<Table.Head class="w-[90px]">Status</Table.Head>
				<Table.Head>Address</Table.Head>
				<Table.Head class="w-[110px]">Last Seen</Table.Head>
				<Table.Head class="w-[110px]">Last Ping</Table.Head>
				<Table.Head class="w-[80px]">Restarts</Table.Head>
				<Table.Head class="w-[70px]">Actions</Table.Head>
			</Table.Row>
		</Table.Header>
		<Table.Body>
			{#if filteredNodes.length === 0}
				<Table.Row>
					<Table.Cell colspan={7} class="h-24 text-center text-muted-foreground">
						{emptyMessage()}
					</Table.Cell>
				</Table.Row>
			{:else}
				{#each filteredNodes as node (node.id)}
					<Table.Row>
						<Table.Cell class="font-medium">
							<a href={`/nodes/${node.id}`} class="hover:underline">{node.name}</a>
						</Table.Cell>
						<Table.Cell>
							{#if node.status === 'Online'}
								<Badge variant="outline" class="border-green-500 text-green-600">Online</Badge>
							{:else}
								<Badge variant="outline" class="border-red-500 text-red-600">Offline</Badge>
							{/if}
						</Table.Cell>
						<Table.Cell class="font-mono text-xs">{node.lastKnownIpAddress}:{node.port}</Table.Cell>
						<Table.Cell>
							{#if node.lastSeenAt}
								<TimeAgo date={node.lastSeenAt} />
							{:else}
								—
							{/if}
						</Table.Cell>
						<Table.Cell>
							{#if node.lastPingAt}
								<TimeAgo date={node.lastPingAt} />
							{:else}
								—
							{/if}
						</Table.Cell>
						<Table.Cell>{node.restartCount}</Table.Cell>
						<Table.Cell>
							<Button
								variant="outline"
								size="icon"
								aria-label="Ping node"
								disabled={pingingId === node.id}
								onclick={() => pingNode(node.id)}
							>
								<Radio />
							</Button>
						</Table.Cell>
					</Table.Row>
				{/each}
			{/if}
		</Table.Body>
	</Table.Root>
{/if}
