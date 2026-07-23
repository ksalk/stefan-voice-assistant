const relativeTimeFormatter = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });

const divisions: Array<{ amount: number; unit: Intl.RelativeTimeFormatUnit }> = [
	{ amount: 60, unit: 'seconds' },
	{ amount: 60, unit: 'minutes' },
	{ amount: 24, unit: 'hours' },
	{ amount: 7, unit: 'days' },
	{ amount: 4.34524, unit: 'weeks' },
	{ amount: 12, unit: 'months' },
	{ amount: Number.POSITIVE_INFINITY, unit: 'years' }
];

export function formatRelativeTime(date: string | Date | null | undefined): string {
	if (date == null) return '';
	const dateObj = typeof date === 'string' ? new Date(date) : date;
	if (Number.isNaN(dateObj.getTime())) return '';

	let duration = (dateObj.getTime() - Date.now()) / 1000;

	for (const division of divisions) {
		if (Math.abs(duration) < division.amount) {
			return relativeTimeFormatter.format(Math.round(duration), division.unit);
		}
		duration /= division.amount;
	}

	return relativeTimeFormatter.format(Math.round(duration), 'years');
}

export function formatDateTime(date: string | Date | null | undefined): string {
	if (date == null) return '';
	const dateObj = typeof date === 'string' ? new Date(date) : date;
	if (Number.isNaN(dateObj.getTime())) return '';

	const day = String(dateObj.getDate()).padStart(2, '0');
	const month = String(dateObj.getMonth() + 1).padStart(2, '0');
	const year = dateObj.getFullYear();
	const hours = String(dateObj.getHours()).padStart(2, '0');
	const minutes = String(dateObj.getMinutes()).padStart(2, '0');
	const seconds = String(dateObj.getSeconds()).padStart(2, '0');

	return `${year}-${month}-${day} ${hours}:${minutes}:${seconds}`;
}
