<script lang="ts">
	import * as Card from '$lib/components/ui/card/index.js';
	import { Button } from '$lib/components/ui/button/index.js';
	import { Skeleton } from '$lib/components/ui/skeleton/index.js';
	import ChevronRight from '@lucide/svelte/icons/chevron-right';
	import Copy from '@lucide/svelte/icons/copy';
	import ScrollText from '@lucide/svelte/icons/scroll-text';
	import { api } from '$lib/api';
	import { toast } from 'svelte-sonner';
	import type { LogEntry } from '$lib/types';

	let { commandId }: { commandId: string } = $props();

	let expanded = $state(false);
	let entries = $state<LogEntry[] | null>(null);
	let loading = $state(false);
	let error: string | null = $state(null);
	let copying = $state(false);

	async function toggle() {
		expanded = !expanded;
		if (expanded && entries === null && !loading) {
			await fetchLogs();
		}
	}

	async function fetchLogs() {
		loading = true;
		error = null;

		try {
			const result = await api.getCommandLogs(commandId);
			entries = [...result.entries].reverse();
		} catch (e) {
			error = e instanceof Error ? e.message : 'Failed to load logs';
		} finally {
			loading = false;
		}
	}

	async function copyLogs() {
		if (!entries?.length) return;
		copying = true;
		try {
			const text = entries.map((entry) => `${entry.timestamp} ${entry.line}`).join('\n');
			await navigator.clipboard.writeText(text);
			toast.success('Copied logs to clipboard');
		} catch {
			toast.error('Failed to copy logs');
		} finally {
			copying = false;
		}
	}

	function formatTimestamp(timestamp: string): string {
		const date = new Date(timestamp);
		return Number.isNaN(date.getTime()) ? timestamp : date.toLocaleString();
	}
</script>

<Card.Root>
	<Card.Header>
		<button
			type="button"
			class="flex w-full items-center gap-2 text-left"
			aria-expanded={expanded}
			onclick={toggle}
		>
			<ScrollText class="size-4 shrink-0 text-muted-foreground" />
			<Card.Title class="text-base">Logs</Card.Title>
			{#if !expanded && entries !== null}
				<span class="text-sm text-muted-foreground">{entries.length} entries</span>
			{/if}
			<ChevronRight
				class="ml-auto size-4 shrink-0 text-muted-foreground transition-transform {expanded
					? 'rotate-90'
					: ''}"
			/>
		</button>
		<Card.Description>Log entries correlated to this command</Card.Description>
	</Card.Header>
	{#if expanded}
		<Card.Content>
			{#if loading}
				<div class="space-y-1.5">
					{#each Array.from({ length: 6 }) as _, i (i)}
						<Skeleton class="h-4 w-full {i % 3 === 2 ? 'w-2/3' : ''}" />
					{/each}
				</div>
			{:else if error}
				<p class="text-sm text-muted-foreground">{error}</p>
			{:else if entries && entries.length > 0}
				<div class="mb-2 flex justify-end">
					<Button variant="outline" size="sm" onclick={copyLogs} disabled={copying}>
						<Copy class="size-4" />
						{copying ? 'Copying...' : 'Copy logs'}
					</Button>
				</div>
				<div class="max-h-96 overflow-y-auto rounded bg-muted/50 p-2 font-mono text-xs">
					{#each entries as entry (entry.timestamp + entry.line)}
						<p class="break-all whitespace-pre-wrap">
							<span class="text-muted-foreground">{formatTimestamp(entry.timestamp)}</span>
							{entry.line}
						</p>
					{/each}
				</div>
			{:else}
				<p class="text-sm text-muted-foreground">No logs found for this command.</p>
			{/if}
		</Card.Content>
	{/if}
</Card.Root>
