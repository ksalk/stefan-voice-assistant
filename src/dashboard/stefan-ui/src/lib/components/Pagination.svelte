<script lang="ts">
	import { Button } from '$lib/components/ui/button/index.js';
	import * as Select from '$lib/components/ui/select/index.js';

	interface Props {
		currentPage: number;
		totalPages: number;
		onPageChange: (page: number) => void;
		pageSize?: number;
		pageSizeOptions?: number[];
		onPageSizeChange?: (size: number) => void;
	}

	let {
		currentPage,
		totalPages,
		onPageChange,
		pageSize,
		pageSizeOptions = [10, 20, 50],
		onPageSizeChange
	}: Props = $props();

	function goToPage(page: number) {
		if (page >= 1 && page <= totalPages) {
			onPageChange(page);
		}
	}

	function pageNumbers(): (number | string)[] {
		if (totalPages <= 7) {
			return Array.from({ length: totalPages }, (_, i) => i + 1);
		}

		const pages: (number | string)[] = [1];
		const start = Math.max(2, currentPage - 1);
		const end = Math.min(totalPages - 1, currentPage + 1);

		if (start > 2) {
			pages.push('...');
		}

		for (let i = start; i <= end; i++) {
			pages.push(i);
		}

		if (end < totalPages - 1) {
			pages.push('...');
		}

		pages.push(totalPages);
		return pages;
	}
</script>

{#if totalPages > 0}
	<div
		class="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 pt-4"
	>
		<div class="flex items-center gap-1">
			<Button
				variant="outline"
				size="icon"
				onclick={() => goToPage(1)}
				disabled={currentPage === 1}
				aria-label="First page"
			>
				≪
			</Button>
			<Button
				variant="outline"
				size="icon"
				onclick={() => goToPage(currentPage - 1)}
				disabled={currentPage === 1}
				aria-label="Previous page"
			>
				‹
			</Button>

			{#each pageNumbers() as page (page)}
				{#if typeof page === 'string'}
					<span class="px-2 text-sm text-muted-foreground">{page}</span>
				{:else}
					<Button
						variant={currentPage === page ? 'default' : 'outline'}
						size="icon"
						onclick={() => goToPage(page)}
						aria-label="Page {page}"
						aria-current={currentPage === page ? 'page' : undefined}
					>
						{page}
					</Button>
				{/if}
			{/each}

			<Button
				variant="outline"
				size="icon"
				onclick={() => goToPage(currentPage + 1)}
				disabled={currentPage >= totalPages}
				aria-label="Next page"
			>
				›
			</Button>
			<Button
				variant="outline"
				size="icon"
				onclick={() => goToPage(totalPages)}
				disabled={currentPage >= totalPages}
				aria-label="Last page"
			>
				≫
			</Button>
		</div>

		<div class="flex items-center gap-3 text-sm text-muted-foreground">
			<span>Page {currentPage} of {totalPages}</span>

			{#if pageSize !== undefined && onPageSizeChange}
				<div class="flex items-center gap-2">
					<span>Rows</span>
					<Select.Root
						type="single"
						value={String(pageSize)}
						onValueChange={(v) => onPageSizeChange(Number(v))}
					>
						<Select.Trigger class="h-8 w-[70px]">{pageSize}</Select.Trigger>
						<Select.Content>
							{#each pageSizeOptions as option (option)}
								<Select.Item value={String(option)}>{option}</Select.Item>
							{/each}
						</Select.Content>
					</Select.Root>
				</div>
			{/if}
		</div>
	</div>
{/if}
