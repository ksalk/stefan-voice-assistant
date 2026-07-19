import type { CommandStatus, StatusGroup } from './types';

type BadgeVariant = 'default' | 'secondary' | 'destructive' | 'outline';

export const statusGroups: { value: StatusGroup; label: string }[] = [
	{ value: 'all', label: 'All' },
	{ value: 'inProgress', label: 'In Progress' },
	{ value: 'completed', label: 'Completed' },
	{ value: 'failed', label: 'Failed' }
];

const inProgressStatuses: CommandStatus[] = ['Received', 'SttSuccess', 'LlmSuccess', 'TtsSuccess'];
const completedStatuses: CommandStatus[] = ['Completed'];
const failedStatuses: CommandStatus[] = [
	'SttFailed',
	'LlmFailed',
	'TtsFailed',
	'HttpFailed',
	'Failed'
];

export function getStatusGroup(status: CommandStatus): StatusGroup {
	if (completedStatuses.includes(status)) return 'completed';
	if (failedStatuses.includes(status)) return 'failed';
	return 'inProgress';
}

export function getStatusesForGroup(group: StatusGroup): CommandStatus[] {
	switch (group) {
		case 'inProgress':
			return inProgressStatuses;
		case 'completed':
			return completedStatuses;
		case 'failed':
			return failedStatuses;
		case 'all':
		default:
			return [...inProgressStatuses, ...completedStatuses, ...failedStatuses];
	}
}

export function getStatusBadgeVariant(status: CommandStatus): BadgeVariant {
	const group = getStatusGroup(status);
	switch (group) {
		case 'completed':
			return 'default';
		case 'failed':
			return 'destructive';
		case 'inProgress':
		default:
			return 'secondary';
	}
}

export function formatDuration(ms: number): string {
	if (ms < 1000) {
		return `${Math.round(ms)}ms`;
	}
	return `${(ms / 1000).toFixed(2)}s`;
}

export function truncate(text: string | null | undefined, maxLength: number = 50): string {
	if (!text) return '';
	if (text.length <= maxLength) return text;
	return text.slice(0, maxLength) + '...';
}
