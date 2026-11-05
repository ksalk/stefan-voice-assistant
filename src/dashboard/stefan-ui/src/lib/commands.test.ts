import { describe, expect, it } from 'vitest';
import { getStatusGroup, getStatusesForGroup } from './commands';

describe('command status grouping', () => {
	it('treats Completed as completed', () => {
		expect(getStatusGroup('Completed')).toBe('completed');
	});

	it('treats legacy TtsSuccess as completed', () => {
		expect(getStatusGroup('TtsSuccess')).toBe('completed');
	});

	it('treats Canceled as failed', () => {
		expect(getStatusGroup('Canceled')).toBe('failed');
	});

	it('has no duplicate statuses in the all filter', () => {
		const statuses = getStatusesForGroup('all');
		expect(new Set(statuses).size).toBe(statuses.length);
	});
});
