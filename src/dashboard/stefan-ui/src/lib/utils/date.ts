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

export function formatRelativeTime(date: string | Date): string {
	const dateObj = typeof date === 'string' ? new Date(date) : date;
	const now = new Date();
	const diffMs = dateObj.getTime() - now.getTime();
	const diffSeconds = diffMs / 1000;

	const absoluteDiff = Math.abs(diffSeconds);

	let unit: Intl.RelativeTimeFormatUnit = 'seconds';
	let value = diffSeconds;

	for (const division of divisions) {
		if (absoluteDiff < division.amount) {
			break;
		}
		value = value / division.amount;
		unit = division.unit;
	}

	// Use singular form for Intl.RelativeTimeFormat
	const singularUnit = unit.endsWith('s')
		? (unit.slice(0, -1) as Intl.RelativeTimeFormatUnit)
		: unit;

	return relativeTimeFormatter.format(Math.round(value), singularUnit);
}

export function formatDateTime(date: string | Date): string {
	const dateObj = typeof date === 'string' ? new Date(date) : date;

	const day = String(dateObj.getDate()).padStart(2, '0');
	const month = String(dateObj.getMonth() + 1).padStart(2, '0');
	const year = dateObj.getFullYear();
	const hours = String(dateObj.getHours()).padStart(2, '0');
	const minutes = String(dateObj.getMinutes()).padStart(2, '0');
	const seconds = String(dateObj.getSeconds()).padStart(2, '0');

	return `${year}-${month}-${day} ${hours}:${minutes}:${seconds}`;
}
