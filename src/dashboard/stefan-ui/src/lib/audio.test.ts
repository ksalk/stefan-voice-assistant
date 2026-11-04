import { describe, expect, it } from 'vitest';
import { extractPeaks, formatAudioTime } from './audio';

describe('extractPeaks', () => {
	it('returns one peak per column', () => {
		expect(extractPeaks(new Float32Array([0, 1, -1, 0.5]), 2)).toHaveLength(2);
	});

	it('finds the minimum and maximum in each column', () => {
		const peaks = extractPeaks(new Float32Array([0.25, -0.5, 1, -1]), 2);

		expect(peaks[0]).toEqual({ min: -0.5, max: 0.25 });
		expect(peaks[1]).toEqual({ min: -1, max: 1 });
	});

	it('normalizes the waveform to the loudest peak', () => {
		const peaks = extractPeaks(new Float32Array([0.5, -0.5, 0.25, -0.25]), 2);

		expect(peaks[0]).toEqual({ min: -1, max: 1 });
		expect(peaks[1]).toEqual({ min: -0.5, max: 0.5 });
	});

	it('returns zero peaks for silence', () => {
		expect(extractPeaks(new Float32Array(10), 2)).toEqual([
			{ min: 0, max: 0 },
			{ min: 0, max: 0 }
		]);
	});

	it('returns an empty array for zero columns', () => {
		expect(extractPeaks(new Float32Array([1]), 0)).toEqual([]);
	});
});

describe('formatAudioTime', () => {
	it('formats seconds with one decimal', () => {
		expect(formatAudioTime(2.5)).toBe('0:02.5');
	});

	it('formats minutes', () => {
		expect(formatAudioTime(75.4)).toBe('1:15.4');
	});

	it('clamps negative and invalid values', () => {
		expect(formatAudioTime(-1)).toBe('0:00.0');
		expect(formatAudioTime(Number.NaN)).toBe('0:00.0');
		expect(formatAudioTime(Number.POSITIVE_INFINITY)).toBe('0:00.0');
	});
});
