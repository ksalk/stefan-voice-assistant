<script lang="ts">
	import { resolve } from '$app/paths';
	import { onMount } from 'svelte';
	import * as Card from '$lib/components/ui/card/index.js';
	import * as Table from '$lib/components/ui/table';
	import { Button } from '$lib/components/ui/button/index.js';
	import { Badge } from '$lib/components/ui/badge/index.js';
	import { Skeleton } from '$lib/components/ui/skeleton/index.js';
	import StatCard from '$lib/components/StatCard.svelte';
	import { getStatusBadgeVariant, truncate } from '$lib/commands';
	import TimeAgo from '$lib/components/TimeAgo.svelte';
	import RefreshCw from '@lucide/svelte/icons/refresh-cw';
	import Radio from '@lucide/svelte/icons/radio';
	import Activity from '@lucide/svelte/icons/activity';
	import CheckCircle2 from '@lucide/svelte/icons/circle-check';
	import Terminal from '@lucide/svelte/icons/terminal';
	import { api } from '$lib/api';
	import type { NodeSummary, Command, GetNodesResult, CommandsResult } from '$lib/types';
	import { toast } from 'svelte-sonner';

	let nodes: NodeSummary[] = $state([]);
	let commands: Command[] = $state([]);
	let loading = $state(true);
	let refreshing = $state(false);
	let nodesError: string | null = $state(null);
	let commandsError: string | null = $state(null);
	let pingingId: string | null = $state(null);

	const onlineCount = $derived(nodes.filter((n) => n.status === 'Online').length);
	const offlineCount = $derived(nodes.filter((n) => n.status === 'Offline').length);
	const offlineNodes = $derived(nodes.filter((n) => n.status === 'Offline'));

	onMount(async () => {
		await fetchDashboard();
	});

	async function fetchDashboard() {
		if (nodes.length === 0 && commands.length === 0) {
			loading = true;
		} else {
			refreshing = true;
		}
		nodesError = null;
		commandsError = null;

		const [nodesResult, commandsResult] = await Promise.allSettled([
			api.getNodes(),
			api.getCommands(1, 10)
		]);

		if (nodesResult.status === 'fulfilled') {
			nodes = (nodesResult.value as GetNodesResult).nodes ?? [];
		} else {
			nodesError =
				nodesResult.reason instanceof Error ? nodesResult.reason.message : 'Failed to load nodes';
		}

		if (commandsResult.status === 'fulfilled') {
			commands = (commandsResult.value as CommandsResult).items ?? [];
		} else {
			commandsError =
				commandsResult.reason instanceof Error
					? commandsResult.reason.message
					: 'Failed to load commands';
		}

		loading = false;
		refreshing = false;
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
				await fetchDashboard();
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
</script>

<div class="flex items-center justify-between">
	<h1 class="text-2xl font-bold">Dashboard</h1>
	<Button
		variant="outline"
		size="sm"
		onclick={() => fetchDashboard()}
		disabled={loading || refreshing}
	>
		<RefreshCw class="mr-2 size-4" />
		Refresh
	</Button>
</div>

{#if loading}
	<div class="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-3">
		{#each [0, 1, 2] as i (i)}
			<Card.Root class="gap-2 p-4">
				<Skeleton class="h-3 w-20" />
				<Skeleton class="mt-2 h-8 w-12" />
			</Card.Root>
		{/each}
	</div>

	<div class="mt-6 grid grid-cols-1 gap-4 lg:grid-cols-3">
		<Card.Root class="col-span-1 lg:col-span-2">
			<Card.Header>
				<Card.Title>Recent Commands</Card.Title>
			</Card.Header>
			<Card.Content>
				<Table.Root>
					<Table.Header>
						<Table.Row>
							<Table.Head>Command</Table.Head>
							<Table.Head>Node</Table.Head>
							<Table.Head class="w-[100px]">Status</Table.Head>
							<Table.Head class="w-[120px]">Received</Table.Head>
						</Table.Row>
					</Table.Header>
					<Table.Body>
						{#each [0, 1, 2, 3] as i (i)}
							<Table.Row>
								<Table.Cell><Skeleton class="h-4 w-48" /></Table.Cell>
								<Table.Cell><Skeleton class="h-4 w-24" /></Table.Cell>
								<Table.Cell><Skeleton class="h-5 w-16 rounded-full" /></Table.Cell>
								<Table.Cell><Skeleton class="h-4 w-16" /></Table.Cell>
							</Table.Row>
						{/each}
					</Table.Body>
				</Table.Root>
			</Card.Content>
		</Card.Root>

		<div class="flex flex-col gap-4">
			<Card.Root class="flex-1">
				<Card.Header>
					<Card.Title>Offline Nodes</Card.Title>
				</Card.Header>
				<Card.Content>
					<div class="space-y-3">
						{#each [0, 1] as i (i)}
							<div class="flex items-center justify-between">
								<Skeleton class="h-4 w-24" />
								<Skeleton class="h-8 w-8 rounded-md" />
							</div>
						{/each}
					</div>
				</Card.Content>
			</Card.Root>

			<Card.Root class="flex-1">
				<Card.Header>
					<Card.Title>Quick Links</Card.Title>
				</Card.Header>
				<Card.Content class="flex flex-col gap-2">
					<Skeleton class="h-9 w-full" />
					<Skeleton class="h-9 w-full" />
				</Card.Content>
			</Card.Root>
		</div>
	</div>
{:else}
	<div class="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-3">
		<StatCard label="Total Nodes" value={nodes.length} class="border-l-4 border-l-slate-500" />
		<StatCard label="Online" value={onlineCount} class="border-l-4 border-l-green-500" />
		<StatCard label="Offline" value={offlineCount} class="border-l-4 border-l-red-500" />
	</div>

	<div class="mt-6 grid grid-cols-1 gap-4 lg:grid-cols-3">
		<Card.Root class="col-span-1 lg:col-span-2">
			<Card.Header class="flex flex-row items-center justify-between">
				<Card.Title class="flex items-center gap-2">
					<Activity class="size-5" />
					Recent Commands
				</Card.Title>
				<Button variant="ghost" size="sm" href="/commands">View all</Button>
			</Card.Header>
			<Card.Content>
				{#if commandsError}
					<p class="text-sm text-red-600">{commandsError}</p>
				{:else if commands.length === 0}
					<p class="text-sm text-muted-foreground">No commands yet.</p>
				{:else}
					<Table.Root>
						<Table.Header>
							<Table.Row>
								<Table.Head>Command</Table.Head>
								<Table.Head>Node</Table.Head>
								<Table.Head class="w-[100px]">Status</Table.Head>
								<Table.Head class="w-[120px]">Received</Table.Head>
							</Table.Row>
						</Table.Header>
						<Table.Body>
							{#each commands.slice(0, 10) as command (command.id)}
								<Table.Row>
									<Table.Cell>
										<a
											href={resolve(`/commands/${command.id}`)}
											class="font-medium hover:underline"
										>
											{truncate(command.transcript)}
										</a>
									</Table.Cell>
									<Table.Cell>
										<a
											href={resolve(`/nodes/${command.nodeId}`)}
											class="font-medium hover:underline"
										>
											{command.nodeName}
										</a>
									</Table.Cell>
									<Table.Cell>
										<Badge variant={getStatusBadgeVariant(command.status)}>{command.status}</Badge>
									</Table.Cell>
									<Table.Cell class="whitespace-nowrap">
										<TimeAgo date={command.receivedAt} />
									</Table.Cell>
								</Table.Row>
							{/each}
						</Table.Body>
					</Table.Root>
				{/if}
			</Card.Content>
		</Card.Root>

		<div class="flex flex-col gap-4">
			<Card.Root class="flex-1">
				<Card.Header>
					<Card.Title>Offline Nodes</Card.Title>
				</Card.Header>
				<Card.Content>
					{#if nodesError}
						<p class="text-sm text-red-600">{nodesError}</p>
					{:else if offlineNodes.length === 0}
						<div class="flex items-center gap-2 text-sm text-green-600">
							<CheckCircle2 class="size-4" />
							All nodes are online
						</div>
					{:else}
						<div class="space-y-3">
							{#each offlineNodes as node (node.id)}
								<div class="flex items-center justify-between gap-2">
									<div class="min-w-0">
										<a
											href={resolve(`/nodes/${node.id}`)}
											class="truncate font-medium hover:underline"
										>
											{node.name}
										</a>
										<p class="text-xs text-muted-foreground">
											{#if node.lastSeenAt}
												<TimeAgo date={node.lastSeenAt} />
											{:else}
												never seen
											{/if}
										</p>
									</div>
									<Button
										variant="outline"
										size="icon"
										aria-label="Ping node"
										disabled={pingingId === node.id}
										onclick={() => pingNode(node.id)}
									>
										<Radio />
									</Button>
								</div>
							{/each}
						</div>
					{/if}
				</Card.Content>
			</Card.Root>

			<Card.Root class="flex-1">
				<Card.Header>
					<Card.Title>Quick Links</Card.Title>
				</Card.Header>
				<Card.Content class="flex flex-col gap-2">
					<Button variant="outline" class="justify-start" href="/nodes">
						<Activity class="mr-2 size-4" />
						Manage Nodes
					</Button>
					<Button variant="outline" class="justify-start" href="/commands">
						<Terminal class="mr-2 size-4" />
						View Commands
					</Button>
				</Card.Content>
			</Card.Root>
		</div>
	</div>
{/if}
