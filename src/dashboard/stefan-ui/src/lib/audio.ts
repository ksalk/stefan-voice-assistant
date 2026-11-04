export interface AudioPeak {
	min: number;
	max: number;
}

let sharedContext: AudioContext | null = null;

export function getAudioContext(): AudioContext {
	if (!sharedContext) {
		sharedContext = new AudioContext();
	}

	return sharedContext;
}

export function downmixToMono(buffer: AudioBuffer): Float32Array {
	if (buffer.numberOfChannels <= 1) {
		return buffer.getChannelData(0);
	}

	const mono = new Float32Array(buffer.length);

	for (let channel = 0; channel < buffer.numberOfChannels; channel++) {
		const data = buffer.getChannelData(channel);
		for (let i = 0; i < data.length; i++) {
			mono[i] += data[i];
		}
	}

	for (let i = 0; i < mono.length; i++) {
		mono[i] /= buffer.numberOfChannels;
	}

	return mono;
}

export function extractPeaks(samples: Float32Array, columns: number): AudioPeak[] {
	if (columns <= 0) {
		return [];
	}

	if (samples.length === 0) {
		return Array.from({ length: columns }, () => ({ min: 0, max: 0 }));
	}

	const peaks: AudioPeak[] = [];
	const samplesPerColumn = samples.length / columns;
	let loudest = 0;

	for (let column = 0; column < columns; column++) {
		const start = Math.floor(column * samplesPerColumn);
		const end = Math.min(samples.length, Math.floor((column + 1) * samplesPerColumn));
		let min = 0;
		let max = 0;

		for (let i = start; i < end; i++) {
			const sample = samples[i];
			if (sample < min) min = sample;
			if (sample > max) max = sample;
		}

		peaks.push({ min, max });
		loudest = Math.max(loudest, Math.abs(min), Math.abs(max));
	}

	if (loudest > 0) {
		for (const peak of peaks) {
			peak.min /= loudest;
			peak.max /= loudest;
		}
	}

	return peaks;
}

export function formatAudioTime(seconds: number): string {
	const safeSeconds = Number.isFinite(seconds) && seconds > 0 ? seconds : 0;
	const tenths = Math.floor(safeSeconds * 10);
	const minutes = Math.floor(tenths / 600);
	const remainder = (tenths % 600) / 10;

	return `${minutes}:${remainder.toFixed(1).padStart(4, '0')}`;
}

let currentAudio: HTMLAudioElement | null = null;

export function registerPlayingAudio(audio: HTMLAudioElement): void {
	if (currentAudio && currentAudio !== audio) {
		currentAudio.pause();
	}

	currentAudio = audio;
}

export function unregisterPlayingAudio(audio: HTMLAudioElement): void {
	if (currentAudio === audio) {
		currentAudio = null;
	}
}
