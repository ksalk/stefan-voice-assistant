<script lang="ts">
	import { onMount } from 'svelte';
	import Download from '@lucide/svelte/icons/download';
	import Pause from '@lucide/svelte/icons/pause';
	import Play from '@lucide/svelte/icons/play';
	import { Button } from '$lib/components/ui/button/index.js';
	import { Skeleton } from '$lib/components/ui/skeleton/index.js';
	import { api } from '$lib/api';
	import { cn } from '$lib/utils.js';
	import {
		downmixToMono,
		extractPeaks,
		formatAudioTime,
		getAudioContext,
		registerPlayingAudio,
		unregisterPlayingAudio,
		type AudioPeak
	} from '$lib/audio';
	import { toast } from 'svelte-sonner';

	type AudioType = 'Request' | 'Response' | 'WakeWord';

	interface Props {
		commandId: string;
		type: AudioType;
		class?: string;
	}

	let { commandId, type, class: className }: Props = $props();

	const TYPE_LABELS: Record<AudioType, string> = {
		Request: 'request',
		Response: 'response',
		WakeWord: 'wake word'
	};

	const TYPE_FILE_SUFFIXES: Record<AudioType, string> = {
		Request: 'request',
		Response: 'response',
		WakeWord: 'wakeword'
	};

	const PEAK_COLUMNS = 800;
	const VIEW_HEIGHT = 100;
	const AMPLITUDE = (VIEW_HEIGHT / 2) * 0.95;

	let status = $state<'loading' | 'ready' | 'error'>('loading');
	let peaks = $state<AudioPeak[]>([]);
	let duration = $state(0);
	let currentTime = $state(0);
	let isPlaying = $state(false);
	let audioUrl = $state<string | null>(null);
	let audioEl: HTMLAudioElement | null = $state(null);
	let waveformEl: HTMLDivElement | null = $state(null);
	let playheadEl: HTMLDivElement | null = $state(null);
	let progressEl: HTMLDivElement | null = $state(null);

	let rafId: number | null = null;
	let lastTenth = -1;
	let cancelled = false;

	const viewBoxWidth = $derived(peaks.length || PEAK_COLUMNS);

	const waveformPath = $derived.by(() => {
		if (peaks.length === 0) {
			return '';
		}

		return peaks
			.map((peak, index) => {
				const x = index + 0.5;
				const top = VIEW_HEIGHT / 2 - peak.max * AMPLITUDE;
				const bottom = VIEW_HEIGHT / 2 - peak.min * AMPLITUDE;
				return `M${x.toFixed(1)} ${top.toFixed(1)}L${x.toFixed(1)} ${bottom.toFixed(1)}`;
			})
			.join('');
	});

	onMount(() => {
		void load();

		return () => {
			cancelled = true;
			stopPlayheadLoop();

			if (audioEl) {
				audioEl.pause();
				unregisterPlayingAudio(audioEl);
			}

			if (audioUrl) {
				URL.revokeObjectURL(audioUrl);
			}
		};
	});

	async function load() {
		try {
			const blob = await api.getCommandAudio(commandId, type);
			if (cancelled) return;

			const audioBuffer = await getAudioContext().decodeAudioData(await blob.arrayBuffer());
			if (cancelled) return;

			peaks = extractPeaks(downmixToMono(audioBuffer), PEAK_COLUMNS);
			duration = audioBuffer.duration;
			audioUrl = URL.createObjectURL(blob);
			status = 'ready';
		} catch (error) {
			if (cancelled) return;

			console.error('Failed to load audio:', error);
			status = 'error';
		}
	}

	async function togglePlayback() {
		if (!audioEl || status !== 'ready') return;

		if (!audioEl.paused) {
			audioEl.pause();
			return;
		}

		const total = audioEl.duration || duration;
		if (audioEl.ended || audioEl.currentTime >= total) {
			audioEl.currentTime = 0;
		}

		registerPlayingAudio(audioEl);

		try {
			await audioEl.play();
		} catch (error) {
			console.error('Failed to play audio:', error);
			toast.error('Failed to play audio');
		}
	}

	function downloadAudio() {
		if (!audioUrl) return;

		const anchor = document.createElement('a');
		anchor.href = audioUrl;
		anchor.download = `command_${commandId}_${TYPE_FILE_SUFFIXES[type]}.wav`;
		document.body.append(anchor);
		anchor.click();
		anchor.remove();
	}

	function handlePlay() {
		isPlaying = true;
		startPlayheadLoop();
	}

	function handlePause() {
		isPlaying = false;
		stopPlayheadLoop();
		updatePlayhead();
	}

	function handleEnded() {
		isPlaying = false;
		stopPlayheadLoop();
		updatePlayhead();
	}

	function startPlayheadLoop() {
		stopPlayheadLoop();

		const tick = () => {
			updatePlayhead();
			rafId = requestAnimationFrame(tick);
		};

		rafId = requestAnimationFrame(tick);
	}

	function stopPlayheadLoop() {
		if (rafId !== null) {
			cancelAnimationFrame(rafId);
			rafId = null;
		}
	}

	function updatePlayhead() {
		if (!audioEl) return;

		const total = audioEl.duration || duration;
		const time = Number.isFinite(audioEl.currentTime) ? audioEl.currentTime : 0;
		const ratio = total > 0 ? Math.min(1, Math.max(0, time / total)) : 0;

		if (playheadEl) playheadEl.style.left = `${ratio * 100}%`;
		if (progressEl) progressEl.style.width = `${ratio * 100}%`;

		const tenth = Math.floor(time * 10);
		if (tenth !== lastTenth) {
			lastTenth = tenth;
			currentTime = time;
		}
	}

	function seek(event: MouseEvent) {
		if (!audioEl || !waveformEl || status !== 'ready') return;

		const total = audioEl.duration || duration;
		if (!Number.isFinite(total) || total <= 0) return;

		const rect = waveformEl.getBoundingClientRect();
		if (rect.width <= 0) return;

		const ratio = Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width));
		audioEl.currentTime = ratio * total;
		updatePlayhead();
	}

	function handleKeydown(event: KeyboardEvent) {
		if (!audioEl || status !== 'ready') return;

		const total = audioEl.duration || duration;
		if (!Number.isFinite(total) || total <= 0) return;

		const step = total * 0.05;

		switch (event.key) {
			case 'ArrowLeft':
				audioEl.currentTime = Math.max(0, audioEl.currentTime - step);
				break;
			case 'ArrowRight':
				audioEl.currentTime = Math.min(total, audioEl.currentTime + step);
				break;
			case 'Home':
				audioEl.currentTime = 0;
				break;
			case 'End':
				audioEl.currentTime = total;
				break;
			case ' ':
			case 'Enter':
				void togglePlayback();
				break;
			default:
				return;
		}

		event.preventDefault();
		updatePlayhead();
	}
</script>

{#if audioUrl}
	<audio
		bind:this={audioEl}
		src={audioUrl}
		preload="auto"
		onplay={handlePlay}
		onpause={handlePause}
		onended={handleEnded}
	></audio>
{/if}

<div class={cn('flex items-center gap-3', className)}>
	<div class="flex shrink-0 flex-col gap-2">
		<Button
			variant="outline"
			size="icon"
			aria-label={`${isPlaying ? 'Pause' : 'Play'} ${TYPE_LABELS[type]} audio`}
			disabled={status !== 'ready'}
			onclick={togglePlayback}
		>
			{#if isPlaying}
				<Pause class="size-4" />
			{:else}
				<Play class="size-4" />
			{/if}
		</Button>

		<Button
			variant="outline"
			size="icon"
			aria-label={`Download ${TYPE_LABELS[type]} audio as WAV`}
			disabled={status !== 'ready'}
			onclick={downloadAudio}
		>
			<Download class="size-4" />
		</Button>
	</div>

	<div class="min-w-0 flex-1">
		{#if status === 'loading'}
			<Skeleton class="h-16 w-full" />
			<Skeleton class="mt-1 h-3 w-24" />
		{:else if status === 'error'}
			<p class="flex h-16 items-center text-sm text-muted-foreground">Audio could not be loaded.</p>
		{:else}
			<div
				bind:this={waveformEl}
				class="relative h-16 w-full cursor-pointer touch-none overflow-hidden rounded-md bg-muted/40 select-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
				role="slider"
				tabindex="0"
				aria-label={`Seek ${TYPE_LABELS[type]} audio`}
				aria-valuemin={0}
				aria-valuemax={duration}
				aria-valuenow={Math.round(currentTime * 10) / 10}
				aria-valuetext={`${formatAudioTime(currentTime)} of ${formatAudioTime(duration)}`}
				onclick={seek}
				onkeydown={handleKeydown}
			>
				<svg
					class="h-full w-full text-muted-foreground"
					viewBox={`0 0 ${viewBoxWidth} ${VIEW_HEIGHT}`}
					preserveAspectRatio="none"
					aria-hidden="true"
				>
					<line
						x1="0"
						x2={viewBoxWidth}
						y1={VIEW_HEIGHT / 2}
						y2={VIEW_HEIGHT / 2}
						class="stroke-border"
						stroke-width="1"
						vector-effect="non-scaling-stroke"
					/>
					<path
						d={waveformPath}
						class="stroke-current"
						fill="none"
						stroke-width="1"
						stroke-linecap="round"
						vector-effect="non-scaling-stroke"
					/>
				</svg>

				<div
					class="pointer-events-none absolute inset-y-0 left-0 bg-primary/10"
					bind:this={progressEl}
				></div>
				<div
					class="pointer-events-none absolute inset-y-0 w-0.5 -translate-x-1/2 bg-primary"
					style="left: 0%"
					bind:this={playheadEl}
				></div>
			</div>

			<div class="mt-1 text-xs text-muted-foreground tabular-nums">
				{formatAudioTime(currentTime)} / {formatAudioTime(duration)}
			</div>
		{/if}
	</div>
</div>
