<script lang="ts">
	import * as Chart from '$lib/components/ui/chart/index.js';
	import { LineChart } from 'layerchart';
	import type { NodeStatusReport } from '$lib/types';
	import { formatDateTime } from '$lib/utils/date';

	type RangeKey = '1h' | '24h' | '7d' | '30d' | 'All';

	interface Props {
		reports: NodeStatusReport[];
	}

	let { reports }: Props = $props();

	const RANGES: { key: RangeKey; label: string; ms: number | null }[] = [
		{ key: '1h', label: '1h', ms: 60 * 60 * 1000 },
		{ key: '24h', label: '24h', ms: 24 * 60 * 60 * 1000 },
		{ key: '7d', label: '7d', ms: 7 * 24 * 60 * 60 * 1000 },
		{ key: '30d', label: '30d', ms: 30 * 24 * 60 * 60 * 1000 },
		{ key: 'All', label: 'All', ms: null }
	];

	let range = $state<RangeKey>('30d');

	const chartConfig = {
		cpuUsage: { label: 'CPU', color: '#ef4444' },
		memoryUsage: { label: 'Memory', color: '#3b82f6' },
		diskUsage: { label: 'Disk', color: '#22c55e' },
		audioVolume: { label: 'Volume', color: '#a855f7' }
	} satisfies Chart.ChartConfig;

	const sortedReports = $derived(
		[...reports].sort((a, b) => +new Date(a.timestamp) - +new Date(b.timestamp))
	);

	const filteredReports = $derived.by(() => {
		const r = RANGES.find((x) => x.key === range);
		if (!r || r.ms === null) return sortedReports;
		const cutoff = Date.now() - r.ms;
		return sortedReports.filter((rep) => +new Date(rep.timestamp) >= cutoff);
	});

	const MAX_POINTS = 300;

	const chartData = $derived.by(() => {
		const rows = filteredReports;
		if (rows.length === 0) return [];
		const step = Math.max(1, Math.ceil(rows.length / MAX_POINTS));
		const out: {
			timestamp: Date;
			cpuUsage: number | null;
			memoryUsage: number | null;
			diskUsage: number | null;
			audioVolume: number | null;
		}[] = [];
		for (let i = 0; i < rows.length; i += step) {
			const rep = rows[i];
			out.push({
				timestamp: new Date(rep.timestamp),
				cpuUsage: rep.cpuUsage,
				memoryUsage: rep.memoryUsage,
				diskUsage: rep.diskUsage,
				audioVolume: rep.audioVolume
			});
		}
		const last = rows[rows.length - 1];
		const lastRow = out[out.length - 1];
		if (lastRow && +lastRow.timestamp !== +new Date(last.timestamp)) {
			out.push({
				timestamp: new Date(last.timestamp),
				cpuUsage: last.cpuUsage,
				memoryUsage: last.memoryUsage,
				diskUsage: last.diskUsage,
				audioVolume: last.audioVolume
			});
		}
		return out;
	});

	const series = [
		{ key: 'cpuUsage', label: chartConfig.cpuUsage.label, color: chartConfig.cpuUsage.color },
		{
			key: 'memoryUsage',
			label: chartConfig.memoryUsage.label,
			color: chartConfig.memoryUsage.color
		},
		{ key: 'diskUsage', label: chartConfig.diskUsage.label, color: chartConfig.diskUsage.color },
		{
			key: 'audioVolume',
			label: chartConfig.audioVolume.label,
			color: chartConfig.audioVolume.color
		}
	];

	function formatXTick(d: Date): string {
		const r = range;
		const dt = new Date(d);
		const pad = (n: number) => n.toString().padStart(2, '0');
		if (r === '1h' || r === '24h') {
			return `${pad(dt.getHours())}:${pad(dt.getMinutes())}`;
		}
		return `${pad(dt.getMonth() + 1)}/${pad(dt.getDate())}`;
	}

	function formatYTick(d: number): string {
		return `${d}%`;
	}
</script>

<div class="flex flex-col gap-3">
	<div class="flex flex-wrap items-center gap-1.5">
		<span class="mr-1 text-xs font-medium text-muted-foreground">Range:</span>
		{#each RANGES as r (r.key)}
			<button
				type="button"
				class="rounded-md border px-2.5 py-1 text-xs font-medium transition-colors {range === r.key
					? 'border-primary bg-primary text-primary-foreground'
					: 'border-border bg-background text-muted-foreground hover:bg-muted hover:text-foreground'}"
				onclick={() => (range = r.key)}
			>
				{r.label}
			</button>
		{/each}
	</div>

	{#if chartData.length === 0}
		<div
			class="flex h-[280px] w-full items-center justify-center rounded-lg border border-dashed border-border text-sm text-muted-foreground"
		>
			No metrics for the selected range.
		</div>
	{:else}
		<Chart.Container config={chartConfig} class="aspect-auto h-[280px] w-full">
			<LineChart
				data={chartData}
				x="timestamp"
				{series}
				yDomain={[0, 100]}
				axis
				legend
				props={{ xAxis: { format: formatXTick }, yAxis: { format: formatYTick } }}
			>
				{#snippet tooltip()}
					<Chart.Tooltip labelFormatter={(value) => formatDateTime(value)} />
				{/snippet}
			</LineChart>
		</Chart.Container>
	{/if}
</div>
