import { describe, expect, it } from 'vitest';
import { trailingAverage, type TimedValue } from './metrics';

const MINUTE = 60 * 1000;

function at(minutes: number, value: number | null): TimedValue {
	return {
		timestamp: new Date(2026, 0, 1, 12, minutes),
		value
	};
}

describe('trailingAverage', () => {
	it('returns an empty array for empty input', () => {
		expect(trailingAverage([], MINUTE)).toEqual([]);
	});

	it('averages points inside the window', () => {
		const points = [at(0, 10), at(1, 20), at(2, 30)];
		expect(trailingAverage(points, 2 * MINUTE)).toEqual([10, 15, 20]);
	});

	it('includes a point exactly one window old', () => {
		const points = [at(0, 10), at(5, 20)];
		expect(trailingAverage(points, 5 * MINUTE)).toEqual([10, 15]);
	});

	it('excludes points older than the window', () => {
		const points = [at(0, 10), at(6, 20)];
		expect(trailingAverage(points, 5 * MINUTE)).toEqual([10, 20]);
	});

	it('skips null values without closing the window', () => {
		const points = [at(0, 10), at(1, null), at(2, 30)];
		expect(trailingAverage(points, 5 * MINUTE)).toEqual([10, 10, 20]);
	});

	it('returns null when the window has no valid values', () => {
		const points = [at(0, null), at(1, null)];
		expect(trailingAverage(points, 5 * MINUTE)).toEqual([null, null]);
	});

	it('does not carry values across a time gap', () => {
		const points = [at(0, 10), at(60, 20)];
		expect(trailingAverage(points, 5 * MINUTE)).toEqual([10, 20]);
	});
});
