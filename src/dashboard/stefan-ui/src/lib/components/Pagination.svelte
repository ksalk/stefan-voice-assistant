<script lang="ts">
	import { Button } from '$lib/components/ui/button/index.js';

	interface Props {
		currentPage: number;
		totalPages: number;
		onPrev: () => void;
		onNext: () => void;
	}

	let { currentPage, totalPages, onPrev, onNext }: Props = $props();
</script>

{#if totalPages > 1}
	<div class="mt-4 flex items-center justify-between border-t border-slate-200 pt-4">
		<Button variant="outline" onclick={onPrev} disabled={currentPage === 1}>Previous</Button>

		<span class="text-sm text-slate-600">
			Page {currentPage} of {totalPages}
		</span>

		<Button variant="outline" onclick={onNext} disabled={currentPage >= totalPages}>Next</Button>
	</div>
{/if}
