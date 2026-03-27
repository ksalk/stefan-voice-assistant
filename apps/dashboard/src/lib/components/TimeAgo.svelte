<script lang="ts">
	import * as Tooltip from '$lib/components/ui/tooltip';
	import { formatRelativeTime, formatDateTime } from '$lib/utils/date';

	interface Props {
		date: string | Date;
	}

	let { date }: Props = $props();

	const relativeTime = $derived(formatRelativeTime(date));
	const formattedDate = $derived(formatDateTime(date));
</script>

<Tooltip.Provider>
	<Tooltip.Root>
		<Tooltip.Trigger>
			{relativeTime}
		</Tooltip.Trigger>
		<Tooltip.Content>
			{formattedDate}
		</Tooltip.Content>
	</Tooltip.Root>
</Tooltip.Provider>
