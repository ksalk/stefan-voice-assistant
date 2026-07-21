<script lang="ts">
	import { onMount } from 'svelte';
	import * as Table from '$lib/components/ui/table';
	import * as Card from '$lib/components/ui/card/index.js';
	import { Button } from '$lib/components/ui/button/index.js';
	import { Badge } from '$lib/components/ui/badge/index.js';
	import { Skeleton } from '$lib/components/ui/skeleton/index.js';
	import StatCard from '$lib/components/StatCard.svelte';
	import Pagination from '$lib/components/Pagination.svelte';
	import NodeMetricsChart from '$lib/components/NodeMetricsChart.svelte';
	import TimeAgo from '$lib/components/TimeAgo.svelte';
	import RefreshCw from '@lucide/svelte/icons/refresh-cw';
	import Radio from '@lucide/svelte/icons/radio';
	import { formatDateTime } from '$lib/utils/date';
	import { api } from '$lib/api';
	import type { Node } from '$lib/types';
	import type { PageProps } from './$types';
	import { toast } from 'svelte-sonner';

	let node = $state<Node | null>(null);
	let loading = $state(true);
	let pinging = $state(false);
	let error: string | null = $state(null);
	let { params }: PageProps = $props();
	let nodeId: string = $derived(params.nodeId);

	let currentPage = $state(1);
	const pageSize = 20;

	const statusReports = $derived(node?.statusReports ?? []);
	const latestReport = $derived(statusReports[0] ?? null);
	const totalCount = $derived(statusReports.length);
	const totalPages = $derived(Math.max(1, Math.ceil(totalCount / pageSize)));
	const pagedReports = $derived(
		statusReports.slice((currentPage - 1) * pageSize, currentPage * pageSize)
	);

	onMount(async () => {
		await fetchNode(nodeId);
	});

	async function fetchNode(id: string) {
		loading = true;
		error = null;
		try {
			const response = await api.getNode(id);
			node = response.node ?? null;
		} catch (e) {
			error = e instanceof Error ? e.message : 'Failed to load node details';
		} finally {
			loading = false;
		}
	}

	async function pingNow() {
		if (!node || pinging) return;
		pinging = true;
		try {
			await api.pingNode(node.id);
			toast.success(`Pinged ${node.name}`);
			try {
				await fetchNode(node.id);
			} catch (e) {
				const msg = e instanceof Error ? e.message : 'Unknown error';
				toast.error(`Failed to refresh ${node.name}: ${msg}`);
			}
		} catch (e) {
			const msg = e instanceof Error ? e.message : 'Unknown error';
			toast.error(`Failed to ping ${node.name}: ${msg}`);
		} finally {
			pinging = false;
		}
	}

	function goToPage(page: number) {
		if (page >= 1 && page <= totalPages) {
			currentPage = page;
		}
	}

	$effect(() => {
		if (currentPage > totalPages) {
			currentPage = totalPages;
		}
	});
</script>

<div class="flex items-center justify-between">
	<h1>Node Details</h1>
	{#if node}
		<div class="flex gap-2">
			<Button variant="outline" onclick={pingNow} disabled={pinging}>
				<Radio class="size-4" />
				{pinging ? 'Pinging…' : 'Ping Now'}
			</Button>
			<Button variant="outline" onclick={() => fetchNode(node!.id)} disabled={loading}>
				<RefreshCw class="size-4" />
				Refresh
			</Button>
		</div>
	{/if}
</div>

{#if loading}
	<div class="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
		{#each [0, 1, 2, 3, 4, 5] as i (i)}
			<Skeleton class="h-[88px] w-full" />
		{/each}
	</div>
	<div class="mt-4 grid gap-4 lg:grid-cols-2">
		<Skeleton class="h-[340px] w-full" />
		<Skeleton class="h-[340px] w-full" />
	</div>
	<Skeleton class="mt-4 h-64 w-full" />
{:else if error}
	<div class="mt-4 rounded border border-red-200 bg-red-50 p-4 text-red-700">
		<p>Error loading node details: {error}</p>
	</div>
{:else if node}
	<!-- KPI tiles -->
	<div class="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
		<Card.Root class="gap-2 p-4">
			<div class="text-xs font-medium text-muted-foreground">Status</div>
			<div class="text-2xl font-semibold">
				{#if node.status === 'Online'}
					<Badge variant="secondary" class="bg-green-100 text-green-700">Online</Badge>
				{:else}
					<Badge variant="destructive">Offline</Badge>
				{/if}
			</div>
		</Card.Root>
		<Card.Root class="gap-2 p-4">
			<div class="text-xs font-medium text-muted-foreground">Last Ping</div>
			<div class="text-base font-medium">
				{#if node.lastPingAt}
					<TimeAgo date={node.lastPingAt} />
				{:else}
					<span class="text-muted-foreground">—</span>
				{/if}
			</div>
		</Card.Root>
		<StatCard label="Restarts" value={node.restartCount} />
		<StatCard label="CPU Used" value={latestReport?.cpuUsage ?? null} unit="%" />
		<StatCard label="Memory Used" value={latestReport?.memoryUsage ?? null} unit="%" />
		<StatCard label="Disk Used" value={latestReport?.diskUsage ?? null} unit="%" />
	</div>

	<!-- Details + metrics -->
	<div class="mt-4 grid gap-4 lg:grid-cols-2">
		<Card.Root>
			<Card.Header>
				<Card.Title>Details</Card.Title>
			</Card.Header>
			<Card.Content>
				<dl class="grid grid-cols-1 gap-x-6 gap-y-3 sm:grid-cols-2">
					<div>
						<dt class="text-xs font-medium text-muted-foreground">Name</dt>
						<dd class="text-sm font-medium text-foreground">{node.name}</dd>
					</div>
					<div>
						<dt class="text-xs font-medium text-muted-foreground">Address</dt>
						<dd class="text-sm font-medium text-foreground">
							{node.lastKnownIpAddress}:{node.port}
						</dd>
					</div>
					<div>
						<dt class="text-xs font-medium text-muted-foreground">Registered</dt>
						<dd class="text-sm font-medium text-foreground">
							{formatDateTime(node.registeredAt)}
						</dd>
					</div>
					<div>
						<dt class="text-xs font-medium text-muted-foreground">Last Seen</dt>
						<dd class="text-sm font-medium text-foreground">
							{formatDateTime(node.lastSeenAt)}
						</dd>
					</div>
					<div>
						<dt class="text-xs font-medium text-muted-foreground">Session</dt>
						<dd class="text-sm font-medium text-foreground">
							{node.currentSessionId || '—'}
						</dd>
					</div>
					<div>
						<dt class="text-xs font-medium text-muted-foreground">Audio Volume</dt>
						<dd class="text-sm font-medium text-foreground">
							{latestReport?.audioVolume ?? '—'}
						</dd>
					</div>
					<div>
						<dt class="text-xs font-medium text-muted-foreground">Version</dt>
						<dd class="text-sm font-medium text-foreground">
							{latestReport?.version ?? '—'}
						</dd>
					</div>
					<div>
						<dt class="text-xs font-medium text-muted-foreground">Git Commit</dt>
						<dd class="text-sm font-medium text-foreground">
							{latestReport?.gitCommit ?? '—'}
						</dd>
					</div>
				</dl>
			</Card.Content>
		</Card.Root>

		<Card.Root>
			<Card.Header>
				<Card.Title>Metrics over time</Card.Title>
			</Card.Header>
			<Card.Content>
				<NodeMetricsChart reports={node.statusReports} />
			</Card.Content>
		</Card.Root>
	</div>

	<!-- Status reports table -->
	<div class="mt-4">
		<Table.Root>
			<Table.Caption>
				Showing {pagedReports.length} of {totalCount} status reports
				{#if totalPages > 1}(Page {currentPage} of {totalPages}){/if}
			</Table.Caption>
			<Table.Header>
				<Table.Row>
					<Table.Head class="w-[150px]">Timestamp</Table.Head>
					<Table.Head>Status</Table.Head>
					<Table.Head>CPU Used %</Table.Head>
					<Table.Head>Memory Used %</Table.Head>
					<Table.Head>Disk Used %</Table.Head>
					<Table.Head>Audio Volume</Table.Head>
					<Table.Head>Version</Table.Head>
					<Table.Head>Git Commit</Table.Head>
				</Table.Row>
			</Table.Header>
			<Table.Body>
				{#each pagedReports as report (report.timestamp)}
					<Table.Row>
						<Table.Cell class="font-medium whitespace-nowrap">
							{formatDateTime(report.timestamp)}
						</Table.Cell>
						<Table.Cell>{report.status}</Table.Cell>
						<Table.Cell>{report.cpuUsage ?? '—'}</Table.Cell>
						<Table.Cell>{report.memoryUsage ?? '—'}</Table.Cell>
						<Table.Cell>{report.diskUsage ?? '—'}</Table.Cell>
						<Table.Cell>{report.audioVolume ?? '—'}</Table.Cell>
						<Table.Cell>{report.version ?? '—'}</Table.Cell>
						<Table.Cell>{report.gitCommit ?? '—'}</Table.Cell>
					</Table.Row>
				{/each}
			</Table.Body>
		</Table.Root>

		<Pagination {currentPage} {totalPages} onPageChange={(page) => goToPage(page)} />
	</div>
{/if}
