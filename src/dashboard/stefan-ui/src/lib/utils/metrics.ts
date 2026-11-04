export interface TimedValue {
	timestamp: Date;
	value: number | null;
}

/**
 * Calculates a trailing average over a time window.
 *
 * Points must be sorted oldest first. For each point, the result is the mean of
 * all non-null values in the inclusive window [timestamp - windowMs, timestamp].
 * A window without valid values produces null. This keeps gaps in the data from
 * pulling old samples into the average.
 */
export function trailingAverage(
	points: readonly TimedValue[],
	windowMs: number
): (number | null)[] {
	const result: (number | null)[] = [];
	let start = 0;
	let sum = 0;
	let count = 0;

	for (let end = 0; end < points.length; end++) {
		const current = points[end];
		if (current.value !== null) {
			sum += current.value;
			count += 1;
		}

		while (current.timestamp.getTime() - points[start].timestamp.getTime() > windowMs) {
			const expired = points[start];
			if (expired.value !== null) {
				sum -= expired.value;
				count -= 1;
			}
			start += 1;
		}

		result.push(count === 0 ? null : sum / count);
	}

	return result;
}
