<script lang="ts">
	import * as Card from '$lib/components/ui/card/index.js';
	import { cn } from '$lib/utils.js';

	interface Props {
		label: string;
		value: string | number | null | undefined;
		unit?: string;
		class?: string;
	}

	let { label, value, unit, class: className }: Props = $props();

	const display = $derived(value === null || value === undefined || value === '' ? '—' : value);
</script>

<Card.Root class={cn('gap-2 p-4', className)}>
	<div class="text-xs font-medium text-muted-foreground">{label}</div>
	<div class="text-2xl font-semibold text-foreground tabular-nums">
		{display}{#if unit && display !== '—'}<span
				class="ml-0.5 text-sm font-normal text-muted-foreground">{unit}</span
			>{/if}
	</div>
</Card.Root>
