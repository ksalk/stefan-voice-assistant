<script lang="ts">
	import { BarChart } from 'layerchart';
	import * as Chart from '$lib/components/ui/chart/index.js';
	import { setChartContext, type ChartConfig } from '$lib/components/ui/chart/chart-utils.js';
	import { formatDuration } from '$lib/commands';
	import type { Command } from '$lib/types';

	interface Props {
		command: Command;
	}

	let { command }: Props = $props();

	const MIN_WIDTH_PCT = 0.02;

	const totalMs = $derived(command.totalDurationMs);
	const actualStt = $derived(command.sttDurationMs ?? 0);
	const actualLlm = $derived(command.llmDurationMs ?? 0);
	const actualTts = $derived(command.ttsDurationMs ?? 0);
	const actualOther = $derived(Math.max(0, totalMs - actualStt - actualLlm - actualTts));

	const status = $derived(command.status);
	const sttFailed = $derived(status === 'SttFailed');
	const llmFailed = $derived(status === 'LlmFailed');
	const ttsFailed = $derived(status === 'TtsFailed');

	const sttNeverRan = $derived(status === 'SttFailed' || status === 'Received');
	const llmNeverRan = $derived(sttNeverRan || status === 'LlmFailed');
	const ttsNeverRan = $derived(llmNeverRan || status === 'TtsFailed');

	const minVisWidth = $derived(totalMs * MIN_WIDTH_PCT);

	const visStt = $derived(sttNeverRan && actualStt === 0 ? minVisWidth : actualStt);
	const visLlm = $derived(llmNeverRan && actualLlm === 0 ? minVisWidth : actualLlm);
	const visTts = $derived(ttsNeverRan && actualTts === 0 ? minVisWidth : actualTts);
	const visOther = $derived(Math.max(0, totalMs - visStt - visLlm - visTts));

	const barData = $derived([
		{ category: 'duration', stt: visStt, llm: visLlm, tts: visTts, other: visOther }
	]);

	const chartConfig = {
		stt: { label: 'STT', color: '#93c5fd' },
		llm: { label: 'LLM', color: '#86efac' },
		tts: { label: 'TTS', color: '#fcd34d' },
		other: { label: 'Other', color: '#d1d5db' }
	} satisfies ChartConfig;

	setChartContext({
		get config() {
			return chartConfig;
		}
	});

	const series = $derived([
		{
			key: 'stt',
			label: 'STT',
			value: (d: Record<string, string | number>) => d.stt as number,
			color: '#93c5fd',
			props: { opacity: sttFailed ? 0.4 : 1 }
		},
		{
			key: 'llm',
			label: 'LLM',
			value: (d: Record<string, string | number>) => d.llm as number,
			color: '#86efac',
			props: { opacity: llmFailed ? 0.4 : 1 }
		},
		{
			key: 'tts',
			label: 'TTS',
			value: (d: Record<string, string | number>) => d.tts as number,
			color: '#fcd34d',
			props: { opacity: ttsFailed ? 0.4 : 1 }
		},
		{
			key: 'other',
			label: 'Other',
			value: (d: Record<string, string | number>) => d.other as number,
			color: '#d1d5db'
		}
	]);

	function actualMs(key: string): number {
		switch (key) {
			case 'stt':
				return actualStt;
			case 'llm':
				return actualLlm;
			case 'tts':
				return actualTts;
			case 'other':
				return actualOther;
		}
		return 0;
	}

	function stageFailed(key: string): boolean {
		switch (key) {
			case 'stt':
				return sttFailed;
			case 'llm':
				return llmFailed;
			case 'tts':
				return ttsFailed;
		}
		return false;
	}
</script>

{#snippet tooltipFormatter(ctx: {
	value: unknown;
	name: string;
	item: { key: string; color?: string };
	index: number;
	payload: unknown[];
})}
	{@const ms = actualMs(ctx.item.key)}
	{@const pct = totalMs > 0 ? ((ms / totalMs) * 100).toFixed(1) : '0'}
	{@const failed = stageFailed(ctx.item.key)}
	<div class="flex items-center gap-2">
		{#if ctx.item.color}
			<div class="size-2.5 shrink-0 rounded-[2px]" style="background-color: {ctx.item.color}"></div>
		{/if}
		<span class="text-muted-foreground">{ctx.name}</span>
		<span class="ml-auto font-mono font-medium tabular-nums">{formatDuration(ms)}</span>
		<span class="text-muted-foreground tabular-nums">({pct}%)</span>
		{#if failed}
			<span class="text-xs text-destructive">Failed</span>
		{/if}
	</div>
{/snippet}

{#if totalMs > 0}
	<div class="mt-3 h-2 w-full min-w-0">
		<BarChart
			data={barData}
			y="category"
			{series}
			seriesLayout="stack"
			orientation="horizontal"
			xDomain={[0, totalMs]}
			padding={0}
		>
			{#snippet tooltip()}
				<Chart.Tooltip hideLabel formatter={tooltipFormatter} />
			{/snippet}
		</BarChart>
	</div>
{/if}
