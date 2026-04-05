<script lang="ts">
	import { onMount } from 'svelte';
	import * as Table from '$lib/components/ui/table';
	import { Button } from '$lib/components/ui/button/index.js';
	import CirclePlay from '@lucide/svelte/icons/circle-play';
	import TimeAgo from '$lib/components/TimeAgo.svelte';
	import { api } from '$lib/api';

	interface Command {
		id: string;
		nodeId: string;
		nodeName: string;
		sessionId: string;
		receivedAt: string;
		inputAudioFormat: string;
		inputAudioDurationMs: number;
		transcript: string;
		responseText: string;
		outputAudioFormat: string;
		sttDurationMs: number;
		llmDurationMs: number;
		ttsDurationMs: number;
		totalDurationMs: number;
		status: string;
		errorMessage: string | null;
	}

	interface CommandsResult {
		items: Command[];
		totalCount: number;
		page: number;
		pageSize: number;
	}

	let commands: Command[] = $state([]);
	let totalCount = $state(0);
	let currentPage = $state(1);
	let pageSize = $state(20);
	let loading = $state(true);
	let error: string | null = $state(null);

	const totalPages = $derived(Math.ceil(totalCount / pageSize));

	onMount(async () => {
		await fetchCommands();
	});

	async function fetchCommands() {
		loading = true;
		error = null;
		try {
			const response: CommandsResult = await api.getCommands(currentPage, pageSize);
			commands = response.items || [];
			totalCount = response.totalCount || 0;
			currentPage = response.page || 1;
			pageSize = response.pageSize || 20;
		} catch (e) {
			error = e instanceof Error ? e.message : 'Failed to load commands';
		} finally {
			loading = false;
		}
	}

	async function goToPage(page: number) {
		if (page >= 1 && page <= totalPages) {
			currentPage = page;
			await fetchCommands();
		}
	}

	function formatDuration(ms: number): string {
		if (ms < 1000) {
			return `${Math.round(ms)}ms`;
		}
		return `${(ms / 1000).toFixed(2)}s`;
	}

	function truncate(text: string, maxLength: number = 50): string {
		if (text.length <= maxLength) return text;
		return text.slice(0, maxLength) + '...';
	}

	function getStatusColor(status: string): string {
		switch (status.toLowerCase()) {
			case 'completed':
				return 'text-green-600';
			case 'failed':
				return 'text-red-600';
			case 'processing':
				return 'text-yellow-600';
			default:
				return 'text-slate-600';
		}
	}
</script>

<h1>Commands</h1>

{#if loading}
	<p class="mt-4 text-slate-600">Loading commands...</p>
{:else if error}
	<div class="mt-4 rounded border border-red-200 bg-red-50 p-4 text-red-700">
		<p>Error loading commands: {error}</p>
	</div>
{:else}
	<div class="mt-4">
		<Table.Root>
			<Table.Caption>
				Showing {commands.length} of {totalCount} commands
				{#if totalPages > 1}
					(Page {currentPage} of {totalPages})
				{/if}
			</Table.Caption>
			<Table.Header>
				<Table.Row>
					<Table.Head class="w-[150px]">Received</Table.Head>
					<Table.Head>Node</Table.Head>
					<Table.Head>Status</Table.Head>
					<Table.Head>Transcript</Table.Head>
					<Table.Head>Response</Table.Head>
					<Table.Head class="w-[100px]">Duration</Table.Head>
					<Table.Head class="w-[80px]">Audio</Table.Head>
				</Table.Row>
			</Table.Header>
			<Table.Body>
				{#each commands as command (command.id)}
					<Table.Row>
						<Table.Cell class="whitespace-nowrap">
							<TimeAgo date={command.receivedAt} />
						</Table.Cell>
						<Table.Cell class="font-medium">{command.nodeName}</Table.Cell>
						<Table.Cell class={getStatusColor(command.status)}>{command.status}</Table.Cell>
						<Table.Cell>{truncate(command.transcript, 40)}</Table.Cell>
						<Table.Cell>{truncate(command.responseText, 40)}</Table.Cell>
						<Table.Cell>{formatDuration(command.totalDurationMs)}</Table.Cell>
						<Table.Cell>
							<Button
								variant="outline"
								size="icon"
								aria-label="Play audio"
								onclick={() => {
									// Audio playback logic to be implemented
									console.log('Play audio for command:', command.id);
								}}
							>
								<CirclePlay />
							</Button>
						</Table.Cell>
					</Table.Row>
				{/each}
			</Table.Body>
		</Table.Root>

		{#if totalPages > 1}
			<div class="mt-4 flex items-center justify-between border-t border-slate-200 pt-4">
				<Button
					variant="outline"
					onclick={() => goToPage(currentPage - 1)}
					disabled={currentPage === 1}
				>
					Previous
				</Button>

				<span class="text-sm text-slate-600">
					Page {currentPage} of {totalPages}
				</span>

				<Button
					variant="outline"
					onclick={() => goToPage(currentPage + 1)}
					disabled={currentPage >= totalPages}
				>
					Next
				</Button>
			</div>
		{/if}
	</div>
{/if}
