<script lang="ts">
	import * as Card from '$lib/components/ui/card/index.js';
	import { Badge } from '$lib/components/ui/badge/index.js';
	import TimeAgo from '$lib/components/TimeAgo.svelte';
	import Wrench from '@lucide/svelte/icons/wrench';
	import ChevronRight from '@lucide/svelte/icons/chevron-right';
	import { SvelteMap } from 'svelte/reactivity';
	import type { CommandTool } from '$lib/types';

	let { tools }: { tools: CommandTool[] } = $props();

	let expandedMap = new SvelteMap<number, boolean>();

	function toggleExpanded(idx: number) {
		expandedMap.set(idx, !(expandedMap.get(idx) ?? false));
	}

	const typeLabels: Record<string, string> = {
		timer: 'Timer',
		'shopping-item': 'Shopping list item'
	};

	const actionBadgeVariants: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
		created: 'default',
		updated: 'secondary',
		deleted: 'destructive'
	};

	function typeLabel(type: string): string {
		return typeLabels[type] ?? type;
	}

	function formatSeconds(seconds: number): string {
		if (seconds < 60) {
			return `${seconds}s`;
		}
		return `${Math.floor(seconds / 60)}m ${seconds % 60}s`;
	}

	function formatPayload(payload: unknown): string {
		if (payload === null || payload === undefined) {
			return '—';
		}
		try {
			return JSON.stringify(payload, null, 2);
		} catch {
			return String(payload);
		}
	}

	function describeTool(tool: CommandTool): string {
		const payload = tool.payload;
		if (!payload || typeof payload !== 'object' || Array.isArray(payload)) {
			return '';
		}
		const p = payload as Record<string, unknown>;
		switch (tool.type) {
			case 'shopping-item':
				return typeof p.name === 'string' ? `"${p.name}"` : '';
			case 'timer': {
				const parts: string[] = [];
				if (typeof p.label === 'string' && p.label) {
					parts.push(`"${p.label}"`);
				}
				if (typeof p.durationInSeconds === 'number') {
					parts.push(formatSeconds(p.durationInSeconds));
				}
				return parts.join(' · ');
			}
			default:
				return JSON.stringify(p);
		}
	}
</script>

<Card.Root>
	<Card.Header>
		<Card.Title class="text-base">Tools</Card.Title>
		<Card.Description>Documents changed by this command</Card.Description>
	</Card.Header>
	<Card.Content class="space-y-2">
		{#each tools as tool, i (i)}
			<div class="rounded-lg border text-sm">
				<button
					type="button"
					class="flex w-full flex-wrap items-center gap-2 rounded-lg p-3 text-left hover:bg-muted/50"
					aria-expanded={expandedMap.get(i) ?? false}
					onclick={() => toggleExpanded(i)}
				>
					<Wrench class="size-4 shrink-0 text-muted-foreground" />
					<Badge variant={actionBadgeVariants[tool.action] ?? 'outline'}>{tool.action}</Badge>
					<span class="font-medium">{typeLabel(tool.type)}</span>
					{#if describeTool(tool)}
						<span class="text-muted-foreground">{describeTool(tool)}</span>
					{/if}
					<span class="ml-auto text-xs text-muted-foreground">
						<TimeAgo date={tool.actionAtUtc} />
					</span>
					<ChevronRight
						class="size-4 shrink-0 text-muted-foreground transition-transform {(expandedMap.get(
							i
						) ?? false)
							? 'rotate-90'
							: ''}"
					/>
				</button>
				{#if expandedMap.get(i)}
					<div class="border-t p-3">
						<p class="mb-1.5 text-xs text-muted-foreground">Payload</p>
						<pre class="overflow-x-auto rounded bg-muted/50 p-2 text-xs">{formatPayload(
								tool.payload
							)}</pre>
					</div>
				{/if}
			</div>
		{/each}
	</Card.Content>
</Card.Root>
