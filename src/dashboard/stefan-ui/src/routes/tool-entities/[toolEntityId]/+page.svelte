<script lang="ts">
	import { resolve } from '$app/paths';
	import * as Card from '$lib/components/ui/card/index.js';
	import * as Alert from '$lib/components/ui/alert/index.js';
	import * as Table from '$lib/components/ui/table';
	import { Button } from '$lib/components/ui/button/index.js';
	import { Badge } from '$lib/components/ui/badge/index.js';
	import { Skeleton } from '$lib/components/ui/skeleton/index.js';
	import TimeAgo from '$lib/components/TimeAgo.svelte';
	import ArrowLeft from '@lucide/svelte/icons/arrow-left';
	import RefreshCw from '@lucide/svelte/icons/refresh-cw';
	import { api } from '$lib/api';
	import type { ToolDocumentDetails } from '$lib/types';
	import type { PageProps } from './$types';

	let document = $state<ToolDocumentDetails | null>(null);
	let loading = $state(true);
	let error: string | null = $state(null);

	let { params }: PageProps = $props();
	let id = $derived(params.toolEntityId);

	$effect(() => {
		if (id) void fetchDocument(id);
	});

	async function fetchDocument(targetId: string) {
		loading = true;
		error = null;
		document = null;
		try {
			const response = await api.getToolDocument(targetId);
			document = response.document ?? null;
		} catch (e) {
			error = e instanceof Error ? e.message : 'Failed to load tool document details';
		} finally {
			loading = false;
		}
	}

	function readProp(payload: unknown, key: string): unknown {
		if (!payload || typeof payload !== 'object' || Array.isArray(payload)) return undefined;
		const p = payload as Record<string, unknown>;
		return p[key] ?? p[key[0].toUpperCase() + key.slice(1)];
	}

	function formatSeconds(seconds: number): string {
		if (seconds < 60) return `${seconds}s`;
		return `${Math.floor(seconds / 60)}m ${seconds % 60}s`;
	}

	const actionBadgeVariants: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
		created: 'default',
		updated: 'secondary',
		deleted: 'destructive'
	};
</script>

{#if loading}
	<div class="flex items-center justify-between">
		<Skeleton class="h-8 w-56" />
		<Skeleton class="h-9 w-32" />
	</div>
	<div class="mt-4 grid gap-4 md:grid-cols-2">
		<Skeleton class="h-48 w-full" />
		<Skeleton class="h-48 w-full" />
	</div>
	<Skeleton class="mt-4 h-64 w-full" />
{:else if error}
	<Alert.Root variant="destructive" class="mt-4">
		<Alert.Title>Error</Alert.Title>
		<Alert.Description>{error}</Alert.Description>
	</Alert.Root>
	<div class="mt-4">
		<Button variant="outline" href={resolve('/tool-entities')}>
			<ArrowLeft class="size-4" />
			Back to Tool Entities
		</Button>
	</div>
{:else if document}
	<div class="flex flex-wrap items-start justify-between gap-3">
		<div>
			<h1>Tool Entity Details</h1>
			<p class="font-mono text-sm break-all text-muted-foreground">{document.id}</p>
		</div>
		<div class="flex gap-2">
			<Button
				variant="outline"
				size="sm"
				onclick={() => fetchDocument(document!.id)}
				disabled={loading}
			>
				<RefreshCw class="size-4" />
				Refresh
			</Button>
			<Button variant="outline" size="sm" href={resolve('/tool-entities')}>
				<ArrowLeft class="size-4" />
				Back
			</Button>
		</div>
	</div>

	<div class="mt-4 flex flex-wrap items-center gap-3">
		<Badge variant="outline">{document.type}</Badge>
		{#if document.isArchived}
			<Badge variant="secondary">Archived</Badge>
		{:else}
			<Badge variant="outline" class="border-green-500 text-green-600">Live</Badge>
		{/if}
		<span class="text-sm text-muted-foreground">
			Created <TimeAgo date={document.createdAt} />
		</span>
		{#if document.archivedAt}
			<span class="text-sm text-muted-foreground">
				· Archived <TimeAgo date={document.archivedAt} />
			</span>
		{/if}
	</div>

	{#if document.type === 'timer'}
		<Card.Root class="mt-4">
			<Card.Header>
				<Card.Title class="text-base">Timer</Card.Title>
			</Card.Header>
			<Card.Content class="space-y-2 text-sm">
				{#if readProp(document.payload, 'label')}
					<p>
						<span class="font-medium">Label:</span>
						{String(readProp(document.payload, 'label'))}
					</p>
				{/if}
				{#if typeof readProp(document.payload, 'durationInSeconds') === 'number'}
					{@const duration = readProp(document.payload, 'durationInSeconds') as number}
					<p><span class="font-medium">Duration:</span> {formatSeconds(duration)}</p>
					<p>
						<span class="font-medium">Expires:</span>
						{new Date(new Date(document.createdAt).getTime() + duration * 1000).toLocaleString()}
					</p>
				{/if}
			</Card.Content>
		</Card.Root>
	{:else if document.type === 'shopping-item'}
		<Card.Root class="mt-4">
			<Card.Header>
				<Card.Title class="text-base">Shopping List Item</Card.Title>
			</Card.Header>
			<Card.Content class="text-sm">
				<p>
					<span class="font-medium">Name:</span>
					{String(readProp(document.payload, 'name') ?? '—')}
				</p>
			</Card.Content>
		</Card.Root>
	{:else if document.type === 'ingredient-list'}
		<Card.Root class="mt-4">
			<Card.Header>
				<Card.Title class="text-base">Ingredient List</Card.Title>
			</Card.Header>
			<Card.Content>
				{#if Array.isArray(readProp(document.payload, 'items')) && (readProp(document.payload, 'items') as unknown[]).length > 0}
					<Table.Root>
						<Table.Header>
							<Table.Row>
								<Table.Head>Name</Table.Head>
								<Table.Head>Quantity</Table.Head>
							</Table.Row>
						</Table.Header>
						<Table.Body>
							{#each (readProp(document.payload, 'items') as unknown[]) ?? [] as item, i (i)}
								<Table.Row>
									<Table.Cell>{String(readProp(item, 'name') ?? '—')}</Table.Cell>
									<Table.Cell>{String(readProp(item, 'quantity') ?? '—')}</Table.Cell>
								</Table.Row>
							{/each}
						</Table.Body>
					</Table.Root>
				{:else}
					<p class="text-sm text-muted-foreground">No ingredients recorded.</p>
				{/if}
			</Card.Content>
		</Card.Root>
	{/if}

	<Card.Root class="mt-4">
		<Card.Header>
			<Card.Title class="text-base">Payload</Card.Title>
		</Card.Header>
		<Card.Content>
			<pre class="overflow-x-auto rounded bg-muted/50 p-3 text-xs">{JSON.stringify(
					document.payload,
					null,
					2
				)}</pre>
		</Card.Content>
	</Card.Root>

	{#if document.commandActions.length > 0}
		<Card.Root class="mt-4">
			<Card.Header>
				<Card.Title class="text-base">Command History</Card.Title>
				<Card.Description>Commands that created, updated or deleted this document</Card.Description>
			</Card.Header>
			<Card.Content>
				<Table.Root>
					<Table.Header>
						<Table.Row>
							<Table.Head>Command</Table.Head>
							<Table.Head class="w-[120px]">Action</Table.Head>
							<Table.Head class="w-[160px]">When</Table.Head>
						</Table.Row>
					</Table.Header>
					<Table.Body>
						{#each document.commandActions as action (action.commandId + action.atUtc)}
							<Table.Row>
								<Table.Cell class="font-mono text-xs">
									<a href={resolve(`/commands/${action.commandId}`)} class="hover:underline">
										{action.commandId}
									</a>
								</Table.Cell>
								<Table.Cell>
									<Badge variant={actionBadgeVariants[action.action] ?? 'outline'}
										>{action.action}</Badge
									>
								</Table.Cell>
								<Table.Cell class="text-sm text-muted-foreground">
									<TimeAgo date={action.atUtc} />
								</Table.Cell>
							</Table.Row>
						{/each}
					</Table.Body>
				</Table.Root>
			</Card.Content>
		</Card.Root>
	{/if}
{/if}
