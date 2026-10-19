<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { onMount, onDestroy } from 'svelte';
	import * as Table from '$lib/components/ui/table';
	import * as Alert from '$lib/components/ui/alert/index.js';
	import * as Select from '$lib/components/ui/select/index.js';
	import { Button } from '$lib/components/ui/button/index.js';
	import { Badge } from '$lib/components/ui/badge/index.js';
	import { Skeleton } from '$lib/components/ui/skeleton/index.js';
	import Pagination from '$lib/components/Pagination.svelte';
	import TimeAgo from '$lib/components/TimeAgo.svelte';
	import RefreshCw from '@lucide/svelte/icons/refresh-cw';
	import Eye from '@lucide/svelte/icons/eye';
	import { api } from '$lib/api';
	import type { ToolDocument } from '$lib/types';

	let items: ToolDocument[] = $state([]);
	let totalCount = $state(0);
	let currentPage = $state(1);
	let pageSize = $state(20);
	let loading = $state(true);
	let refreshing = $state(false);
	let error: string | null = $state(null);

	let scope = $state<'live' | 'archived' | 'all'>('live');
	let typeFilter = $state('');
	let search = $state('');
	let sortBy = $state('createdAt');
	let sortDirection = $state('desc');

	let searchTimer: ReturnType<typeof setTimeout> | null = null;

	const totalPages = $derived(Math.max(1, Math.ceil(totalCount / pageSize)));

	const typeLabels: Record<string, string> = {
		timer: 'Timer',
		'shopping-item': 'Shopping list item',
		'ingredient-list': 'Ingredient list'
	};

	onMount(async () => {
		await fetchDocuments();
	});

	onDestroy(() => {
		if (searchTimer) clearTimeout(searchTimer);
	});

	async function fetchDocuments(resetPage = false) {
		if (resetPage) currentPage = 1;
		loading = items.length === 0;
		refreshing = items.length > 0;
		error = null;
		try {
			const response = await api.getToolDocuments(currentPage, pageSize, {
				scope,
				type: typeFilter || undefined,
				search: search.trim() || undefined,
				sortBy,
				sortDirection
			});
			items = response.items || [];
			totalCount = response.totalCount || 0;
			currentPage = response.page || 1;
			pageSize = response.pageSize || 20;
		} catch (e) {
			console.error('Error fetching tool documents:', e);
			error = e instanceof Error ? e.message : 'Failed to load tool documents';
		} finally {
			loading = false;
			refreshing = false;
		}
	}

	async function refetch(resetPage = false) {
		await fetchDocuments(resetPage);
	}

	async function goToPage(page: number) {
		currentPage = page;
		await fetchDocuments();
	}

	async function handlePageSizeChange(size: number) {
		pageSize = size;
		currentPage = 1;
		await fetchDocuments();
	}

	function handleScopeChange(value: string) {
		scope = value as 'live' | 'archived' | 'all';
		refetch(true);
	}

	function handleTypeChange(value: string) {
		typeFilter = value === '__all__' ? '' : value;
		refetch(true);
	}

	function handleSortByChange(value: string) {
		sortBy = value;
		refetch(true);
	}

	function handleSortDirectionChange(value: string) {
		sortDirection = value;
		refetch(true);
	}

	function handleSearchInput(value: string) {
		search = value;
		if (searchTimer) clearTimeout(searchTimer);
		searchTimer = setTimeout(() => {
			refetch(true);
		}, 300);
	}

	function typeLabel(type: string): string {
		return typeLabels[type] ?? type;
	}

	function describeDocument(doc: ToolDocument): string {
		const payload = doc.payload;
		if (!payload || typeof payload !== 'object' || Array.isArray(payload)) {
			return '';
		}
		const p = payload as Record<string, unknown>;
		const read = (key: string): unknown => p[key] ?? p[key[0].toUpperCase() + key.slice(1)];
		switch (doc.type) {
			case 'shopping-item':
				return typeof read('name') === 'string' ? `"${read('name')}"` : '';
			case 'ingredient-list': {
				const items = read('items');
				const count = Array.isArray(items) ? items.length : 0;
				return count === 0 ? 'empty' : `${count} ingredient${count === 1 ? '' : 's'}`;
			}
			case 'timer': {
				const parts: string[] = [];
				const label = read('label');
				if (typeof label === 'string' && label) parts.push(`"${label}"`);
				const duration = read('durationInSeconds');
				if (typeof duration === 'number') {
					parts.push(
						duration < 60 ? `${duration}s` : `${Math.floor(duration / 60)}m ${duration % 60}s`
					);
				}
				return parts.join(' · ');
			}
			default:
				try {
					return JSON.stringify(payload).slice(0, 80);
				} catch {
					return '';
				}
		}
	}

	function emptyMessage(): string {
		if (search.trim()) return 'No tool documents match the search.';
		if (typeFilter) return `No ${typeLabel(typeFilter)} documents.`;
		if (scope === 'archived') return 'No archived tool documents.';
		return 'No tool documents yet.';
	}

	async function openDetails(id: string) {
		await goto(resolve(`/tool-entities/${id}`));
	}
</script>

<h1>Tool Entities</h1>

<div class="mt-4 flex flex-wrap items-center justify-between gap-3">
	<div class="flex flex-wrap items-center gap-3">
		<Select.Root type="single" value={scope} onValueChange={handleScopeChange}>
			<Select.Trigger class="w-[140px]">
				{scope === 'live' ? 'Live' : scope === 'archived' ? 'Archived' : 'All'}
			</Select.Trigger>
			<Select.Content>
				<Select.Item value="live">Live</Select.Item>
				<Select.Item value="archived">Archived</Select.Item>
				<Select.Item value="all">All</Select.Item>
			</Select.Content>
		</Select.Root>

		<Select.Root type="single" value={typeFilter || '__all__'} onValueChange={handleTypeChange}>
			<Select.Trigger class="w-[200px]"
				>{typeFilter ? typeLabel(typeFilter) : 'All types'}</Select.Trigger
			>
			<Select.Content>
				<Select.Item value="__all__">All types</Select.Item>
				<Select.Item value="timer">Timer</Select.Item>
				<Select.Item value="shopping-item">Shopping list item</Select.Item>
				<Select.Item value="ingredient-list">Ingredient list</Select.Item>
			</Select.Content>
		</Select.Root>

		<input
			class="h-9 w-[220px] rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm"
			placeholder="Search id, type or payload…"
			value={search}
			oninput={(e) => handleSearchInput((e.currentTarget as HTMLInputElement).value)}
		/>

		<Select.Root type="single" value={sortBy} onValueChange={handleSortByChange}>
			<Select.Trigger class="w-[130px]">
				Sort: {sortBy === 'createdAt' ? 'Created' : sortBy === 'type' ? 'Type' : 'ID'}
			</Select.Trigger>
			<Select.Content>
				<Select.Item value="createdAt">Created</Select.Item>
				<Select.Item value="type">Type</Select.Item>
				<Select.Item value="id">ID</Select.Item>
			</Select.Content>
		</Select.Root>

		<Select.Root type="single" value={sortDirection} onValueChange={handleSortDirectionChange}>
			<Select.Trigger class="w-[100px]">{sortDirection === 'desc' ? 'Desc' : 'Asc'}</Select.Trigger>
			<Select.Content>
				<Select.Item value="desc">Desc</Select.Item>
				<Select.Item value="asc">Asc</Select.Item>
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

{#if error}
	<Alert.Root variant="destructive" class="mt-4">
		<Alert.Title>Error</Alert.Title>
		<Alert.Description>{error}</Alert.Description>
	</Alert.Root>
{:else}
	<Table.Root class="mt-4">
		<Table.Caption>
			{#if loading}
				Loading tool documents…
			{:else}
				Showing {items.length} of {totalCount} tool documents
				{#if totalPages > 1}(page {currentPage} of {totalPages}){/if}
			{/if}
		</Table.Caption>
		<Table.Header>
			<Table.Row>
				<Table.Head>ID</Table.Head>
				<Table.Head>Type</Table.Head>
				<Table.Head>Summary</Table.Head>
				<Table.Head class="w-[150px]">Created</Table.Head>
				<Table.Head class="w-[110px]">Status</Table.Head>
				<Table.Head class="w-[70px]">View</Table.Head>
			</Table.Row>
		</Table.Header>
		<Table.Body>
			{#if loading}
				{#each [0, 1, 2, 3, 4] as i (i)}
					<Table.Row>
						<Table.Cell><Skeleton class="h-4 w-40" /></Table.Cell>
						<Table.Cell><Skeleton class="h-4 w-24" /></Table.Cell>
						<Table.Cell><Skeleton class="h-4 w-40" /></Table.Cell>
						<Table.Cell><Skeleton class="h-4 w-24" /></Table.Cell>
						<Table.Cell><Skeleton class="h-5 w-20 rounded-full" /></Table.Cell>
						<Table.Cell><Skeleton class="h-8 w-8 rounded-md" /></Table.Cell>
					</Table.Row>
				{/each}
			{:else if items.length === 0}
				<Table.Row>
					<Table.Cell colspan={6} class="h-24 text-center text-muted-foreground">
						{emptyMessage()}
					</Table.Cell>
				</Table.Row>
			{:else}
				{#each items as doc (doc.id + String(doc.isArchived))}
					<Table.Row class="cursor-pointer hover:bg-muted/50" onclick={() => openDetails(doc.id)}>
						<Table.Cell class="font-mono text-xs">
							<a
								href={resolve(`/tool-entities/${doc.id}`)}
								class="hover:underline"
								onclick={(e) => e.stopPropagation()}
							>
								{doc.id}
							</a>
						</Table.Cell>
						<Table.Cell>
							<Badge variant="outline">{typeLabel(doc.type)}</Badge>
						</Table.Cell>
						<Table.Cell class="text-sm text-muted-foreground">
							{describeDocument(doc)}
						</Table.Cell>
						<Table.Cell class="whitespace-nowrap">
							<TimeAgo date={doc.createdAt} />
						</Table.Cell>
						<Table.Cell>
							{#if doc.isArchived}
								<Badge variant="secondary">Archived</Badge>
							{:else}
								<Badge variant="outline" class="border-green-500 text-green-600">Live</Badge>
							{/if}
						</Table.Cell>
						<Table.Cell>
							<Button
								variant="outline"
								size="icon"
								aria-label="View tool document"
								href={`/tool-entities/${doc.id}`}
								onclick={(e) => e.stopPropagation()}
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
