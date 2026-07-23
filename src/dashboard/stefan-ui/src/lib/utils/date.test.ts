import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import { formatDateTime, formatRelativeTime } from './date';

describe('formatDateTime', () => {
	it('formats a fixed UTC date', () => {
		const result = formatDateTime(new Date('2025-01-15T09:30:45Z'));
		expect(result).toBe('2025-01-15 09:30:45');
	});

	it('returns empty for null and undefined', () => {
		expect(formatDateTime(null)).toBe('');
		expect(formatDateTime(undefined)).toBe('');
	});

	it('returns empty for invalid date strings', () => {
		expect(formatDateTime('not a date')).toBe('');
	});
});

describe('formatRelativeTime', () => {
	beforeEach(() => {
		vi.useFakeTimers();
		vi.setSystemTime(new Date('2025-06-15T12:00:00Z'));
	});

	afterEach(() => {
		vi.useRealTimers();
	});

	const offset = (ms: number) => new Date(Date.now() + ms);

	it('returns empty for null, undefined, and invalid strings', () => {
		expect(formatRelativeTime(null)).toBe('');
		expect(formatRelativeTime(undefined)).toBe('');
		expect(formatRelativeTime('not a date')).toBe('');
	});

	it('formats seconds', () => {
		expect(formatRelativeTime(offset(-30_000))).toBe('30 seconds ago');
		expect(formatRelativeTime(offset(30_000))).toBe('in 30 seconds');
	});

	it('formats minutes', () => {
		expect(formatRelativeTime(offset(-120_000))).toBe('2 minutes ago');
		expect(formatRelativeTime(offset(120_000))).toBe('in 2 minutes');
	});

	it('formats hours', () => {
		expect(formatRelativeTime(offset(-3_600_000))).toBe('1 hour ago');
		expect(formatRelativeTime(offset(3_600_000))).toBe('in 1 hour');
	});

	it('formats yesterday and tomorrow', () => {
		expect(formatRelativeTime(offset(-86_400_000))).toBe('yesterday');
		expect(formatRelativeTime(offset(86_400_000))).toBe('tomorrow');
	});

	it('formats days', () => {
		expect(formatRelativeTime(offset(-172_800_000))).toBe('2 days ago');
		expect(formatRelativeTime(offset(172_800_000))).toBe('in 2 days');
	});

	it('formats weeks', () => {
		expect(formatRelativeTime(offset(-7 * 86_400_000))).toBe('last week');
		expect(formatRelativeTime(offset(7 * 86_400_000))).toBe('next week');
	});

	it('formats months', () => {
		expect(formatRelativeTime(offset(-40 * 86_400_000))).toBe('last month');
		expect(formatRelativeTime(offset(40 * 86_400_000))).toBe('next month');
	});

	it('formats years', () => {
		expect(formatRelativeTime(offset(-366 * 86_400_000))).toBe('last year');
		expect(formatRelativeTime(offset(366 * 86_400_000))).toBe('next year');
	});
});
