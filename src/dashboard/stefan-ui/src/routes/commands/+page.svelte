<script lang="ts">
	import { resolve } from '$app/paths';
	import { onMount } from 'svelte';
	import * as Table from '$lib/components/ui/table';
	import * as Alert from '$lib/components/ui/alert/index.js';
	import * as ToggleGroup from '$lib/components/ui/toggle-group/index.js';
	import * as Select from '$lib/components/ui/select/index.js';
	import { Button } from '$lib/components/ui/button/index.js';
	import { Badge } from '$lib/components/ui/badge/index.js';
	import { Skeleton } from '$lib/components/ui/skeleton/index.js';
	import Pagination from '$lib/components/Pagination.svelte';
	import TimeAgo from '$lib/components/TimeAgo.svelte';
	import RefreshCw from '@lucide/svelte/icons/refresh-cw';
	import Mic from '@lucide/svelte/icons/mic';
	import Volume2 from '@lucide/svelte/icons/volume-2';
	import Eye from '@lucide/svelte/icons/eye';
	import { api } from '$lib/api';
	import {
		formatDuration,
		getStatusBadgeVariant,
		getStatusesForGroup,
		statusGroups,
		truncate
	} from '$lib/commands';
	import { toast } from 'svelte-sonner';
	import type { Command, NodeSummary, StatusGroup } from '$lib/types';

	let commands: Command[] = $state([]);
	let totalCount = $state(0);
	let currentPage = $state(1);
	let pageSize = $state(20);
	let loading = $state(true);
	let refreshing = $state(false);
	let error: string | null = $state(null);
	let statusGroup = $state<StatusGroup>('all');
	let nodeId = $state<string>('');
	let nodes: NodeSummary[] = $state([]);
	let nodesLoading = $state(true);
	let nodesError: string | null = $state(null);

	const totalPages = $derived(Math.max(1, Math.ceil(totalCount / pageSize)));

	onMount(async () => {
		await Promise.all([fetchNodes(), fetchCommands()]);
	});

	async function fetchNodes() {
		nodesLoading = true;
		nodesError = null;
		try {
			const response = await api.getNodes();
			nodes = response.nodes ?? [];
		} catch (e) {
			nodesError = e instanceof Error ? e.message : 'Failed to load nodes';
		} finally {
			nodesLoading = false;
		}
	}

	async function fetchCommands() {
		loading = commands.length === 0;
		refreshing = commands.length > 0;
		error = null;
		try {
			const response = await api.getCommands(currentPage, pageSize, {
				nodeId: nodeId || undefined,
				statuses: getStatusesForGroup(statusGroup)
			});
			commands = response.items || [];
			totalCount = response.totalCount || 0;
			currentPage = response.page || 1;
			pageSize = response.pageSize || 20;
		} catch (e) {
			console.error('Error fetching commands:', e);
			error = e instanceof Error ? e.message : 'Failed to load commands';
		} finally {
			loading = false;
			refreshing = false;
		}
	}

	async function refetch(resetPage = false) {
		if (resetPage) currentPage = 1;
		await fetchCommands();
	}

	async function goToPage(page: number) {
		currentPage = page;
		await fetchCommands();
	}

	async function handlePageSizeChange(size: number) {
		pageSize = size;
		currentPage = 1;
		await fetchCommands();
	}

	function selectedNodeLabel(): string {
		if (!nodeId) return 'All nodes';
		return nodes.find((n) => n.id === nodeId)?.name ?? 'Unknown node';
	}

	function emptyMessage(): string {
		if (statusGroup !== 'all' && nodeId) {
			return 'No commands match the selected status and node.';
		}
		if (statusGroup !== 'all') {
			return `No ${statusGroup === 'inProgress' ? 'in progress' : statusGroup} commands.`;
		}
		if (nodeId) {
			return 'No commands for the selected node.';
		}
		return 'No commands recorded yet.';
	}

	async function playAudio(commandId: string, type: 'Request' | 'Response') {
		try {
			const blob = await api.getCommandAudio(commandId, type);
			const url = URL.createObjectURL(blob);
			const audio = new Audio(url);
			audio.onended = () => URL.revokeObjectURL(url);
			audio.onerror = () => URL.revokeObjectURL(url);
			await audio.play();
		} catch (e) {
			console.error('Failed to play audio:', e);
			toast.error('Failed to play audio: ' + (e instanceof Error ? e.message : 'Unknown error'));
		}
	}
</script>

<h1>Commands</h1>

<div class="mt-4 flex flex-wrap items-center justify-between gap-3">
	<div class="flex flex-wrap items-center gap-3">
		<ToggleGroup.Root
			type="single"
			value={statusGroup}
			variant="outline"
			onValueChange={(v) => {
				const next = (v as StatusGroup) || statusGroup;
				if (next === statusGroup) return;
				statusGroup = next;
				refetch(true);
			}}
		>
			{#each statusGroups as group (group.value)}
				<ToggleGroup.Item value={group.value} aria-label="Show {group.label} commands">
					{group.label}
				</ToggleGroup.Item>
			{/each}
		</ToggleGroup.Root>

		<Select.Root
			type="single"
			value={nodeId}
			onValueChange={(v) => {
				nodeId = v;
				refetch(true);
			}}
			disabled={nodesLoading || nodes.length === 0}
		>
			<Select.Trigger class="w-[180px]">{selectedNodeLabel()}</Select.Trigger>
			<Select.Content>
				<Select.Item value="">All nodes</Select.Item>
				{#each nodes as node (node.id)}
					<Select.Item value={node.id}>{node.name}</Select.Item>
				{/each}
			</Select.Content>
		</Select.Root>

		<span class="text-sm text-muted-foreground">
			{#if loading}
				…
			{:else}
				{totalCount} total
			{/if}
		</span>
	</div>

	<Button variant="outline" size="sm" onclick={() => refetch()} disabled={loading || refreshing}>
		<RefreshCw class="size-4" />
		Refresh
	</Button>
</div>

{#if nodesError}
	<Alert.Root variant="destructive" class="mt-4">
		<Alert.Title>Node filter unavailable</Alert.Title>
		<Alert.Description>{nodesError}</Alert.Description>
	</Alert.Root>
{/if}

{#if loading}
	<Table.Root class="mt-4">
		<Table.Header>
			<Table.Row>
				<Table.Head>Command</Table.Head>
				<Table.Head>Node</Table.Head>
				<Table.Head class="w-[120px]">Status</Table.Head>
				<Table.Head class="w-[150px]">Received</Table.Head>
				<Table.Head>Response</Table.Head>
				<Table.Head class="w-[100px]">Duration</Table.Head>
				<Table.Head class="w-[100px]">Audio</Table.Head>
				<Table.Head class="w-[70px]">Actions</Table.Head>
			</Table.Row>
		</Table.Header>
		<Table.Body>
			{#each [0, 1, 2, 3, 4] as i (i)}
				<Table.Row>
					<Table.Cell><Skeleton class="h-4 w-40" /></Table.Cell>
					<Table.Cell><Skeleton class="h-4 w-24" /></Table.Cell>
					<Table.Cell><Skeleton class="h-5 w-20 rounded-full" /></Table.Cell>
					<Table.Cell><Skeleton class="h-4 w-24" /></Table.Cell>
					<Table.Cell><Skeleton class="h-4 w-40" /></Table.Cell>
					<Table.Cell><Skeleton class="h-4 w-16" /></Table.Cell>
					<Table.Cell>
						<div class="flex gap-1">
							<Skeleton class="h-8 w-8 rounded-md" />
							<Skeleton class="h-8 w-8 rounded-md" />
						</div>
					</Table.Cell>
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
		<Table.Caption>
			Showing {commands.length} of {totalCount} commands
			{#if totalPages > 1}(page {currentPage} of {totalPages}){/if}
		</Table.Caption>
		<Table.Header>
			<Table.Row>
				<Table.Head>Command</Table.Head>
				<Table.Head>Node</Table.Head>
				<Table.Head class="w-[120px]">Status</Table.Head>
				<Table.Head class="w-[150px]">Received</Table.Head>
				<Table.Head>Response</Table.Head>
				<Table.Head class="w-[100px]">Duration</Table.Head>
				<Table.Head class="w-[100px]">Audio</Table.Head>
				<Table.Head class="w-[70px]">Actions</Table.Head>
			</Table.Row>
		</Table.Header>
		<Table.Body>
			{#if commands.length === 0}
				<Table.Row>
					<Table.Cell colspan={8} class="h-24 text-center text-muted-foreground">
						{emptyMessage()}
					</Table.Cell>
				</Table.Row>
			{:else}
				{#each commands as command (command.id)}
					<Table.Row>
						<Table.Cell>
							<a href={resolve(`/commands/${command.id}`)} class="font-medium hover:underline">
								{command.transcript ? truncate(command.transcript, 40) : '(no transcript)'}
							</a>
						</Table.Cell>
						<Table.Cell>{command.nodeName}</Table.Cell>
						<Table.Cell>
							<Badge variant={getStatusBadgeVariant(command.status)}>{command.status}</Badge>
						</Table.Cell>
						<Table.Cell class="whitespace-nowrap">
							<TimeAgo date={command.receivedAt} />
						</Table.Cell>
						<Table.Cell>{truncate(command.responseText, 40)}</Table.Cell>
						<Table.Cell>{formatDuration(command.totalDurationMs)}</Table.Cell>
						<Table.Cell>
							<div class="flex gap-1">
								<Button
									variant="outline"
									size="icon"
									aria-label="Play request audio"
									onclick={() => playAudio(command.id, 'Request')}
								>
									<Mic class="size-4" />
								</Button>
								<Button
									variant="outline"
									size="icon"
									aria-label="Play response audio"
									onclick={() => playAudio(command.id, 'Response')}
								>
									<Volume2 class="size-4" />
								</Button>
							</div>
						</Table.Cell>
						<Table.Cell>
							<Button
								variant="outline"
								size="icon"
								aria-label="View command details"
								href={`/commands/${command.id}`}
							>
								<Eye class="size-4" />
							</Button>
						</Table.Cell>
					</Table.Row>
				{/each}
			{/if}
		</Table.Body>
	</Table.Root>

	<Pagination
		{currentPage}
		{totalPages}
		onPageChange={goToPage}
		{pageSize}
		pageSizeOptions={[10, 20, 50]}
		onPageSizeChange={handlePageSizeChange}
	/>
{/if}
