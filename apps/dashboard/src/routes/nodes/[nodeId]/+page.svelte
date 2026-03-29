<script lang="ts">
	import { onMount } from 'svelte';
	import * as Table from '$lib/components/ui/table';
	import { Button } from '$lib/components/ui/button/index.js';
	import CirclePlay from '@lucide/svelte/icons/circle-play';
	import TimeAgo from '$lib/components/TimeAgo.svelte';
	import { formatDateTime } from '$lib/utils/date';
	import { api } from '$lib/api';
	import type { PageProps } from '../$types';

	let node: any = $state({});
	let loading = $state(true);
	let error: string | null = $state(null);
    let { params }: PageProps = $props();
    let nodeId: string = $derived(params.nodeId);

	onMount(async () => {
        // fetch Id from Route param
		await fetchNode(nodeId);
	});

	async function fetchNode(id: string) {
		try {
			const response = await api.getNode(id);
			node = response.node || {};
		} catch (e) {
			error = e instanceof Error ? e.message : 'Failed to load node details';
		} finally {
			loading = false;
		}
	}
</script>

<h1>Node Details</h1>

{#if loading}
    <p class="mt-4 text-slate-600">Loading node details...</p>
{:else if error}
    <div class="mt-4 rounded border border-red-200 bg-red-50 p-4 text-red-700">
        <p>Error loading node details: {error}</p>
    </div>
{:else}
    <div class="mt-4 space-y-4">
        <div>
            <h2 class="text-lg font-medium">Name: {node.name}</h2>
            <p>Status: {node.status}</p>
            <p>IP: {node.lastKnownIpAddress}:{node.port}</p>
            <p>Registered: {formatDateTime(node.registeredAt)}</p>
            <p>Last Ping: {formatDateTime(node.lastPingAt)}</p>
        </div>
    </div>

    <div class="mt-4 space-y-4">
        <Table.Root class="mt-4">
		<Table.Caption>Node status reports.</Table.Caption>
		<Table.Header>
			<Table.Row>
				<Table.Head class="w-[100px]">Timestamp</Table.Head>
				<Table.Head>Status</Table.Head>
				<Table.Head>CpuUsage</Table.Head>
				<Table.Head>MemoryUsage</Table.Head>
				<Table.Head>DiskUsage</Table.Head>
			</Table.Row>
		</Table.Header>
		<Table.Body>
			{#each node.statusReports as report (report.timestamp)}
				<Table.Row>
					<Table.Cell class="font-medium">{formatDateTime(report.timestamp)}</Table.Cell>
					<Table.Cell>{report.status}</Table.Cell>
					<Table.Cell>{report.cpuUsage}</Table.Cell>
					<Table.Cell>{report.memoryUsage}</Table.Cell>
					<Table.Cell>{report.diskUsage}</Table.Cell>
				</Table.Row>
			{/each}
		</Table.Body>
	</Table.Root>
    </div>
{/if}