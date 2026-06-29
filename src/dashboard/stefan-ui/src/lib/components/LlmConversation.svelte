<script lang="ts">
	import * as Card from '$lib/components/ui/card/index.js';
	import Bot from '@lucide/svelte/icons/bot';
	import User from '@lucide/svelte/icons/user';
	import Terminal from '@lucide/svelte/icons/terminal';
	import Zap from '@lucide/svelte/icons/zap';
	import ChevronRight from '@lucide/svelte/icons/chevron-right';
	import type { ConversationMessage } from '$lib/types';

	let { llmConversationJson }: { llmConversationJson: string | null } = $props();

	let systemExpanded = $state(false);
	let parsedMessages = $state<ConversationMessage[]>([]);

	const roleLabels: Record<string, string> = {
		system: 'System prompt',
		user: 'User input',
		assistant: 'Assistant'
	};

	interface RawToolCall {
		Id: string;
		FunctionName: string;
		Arguments: string;
		Result: string | null;
	}

	interface RawMessage {
		Role: string;
		Content: string | null;
		ToolCalls: RawToolCall[] | null;
	}

	function parseConversation() {
		if (!llmConversationJson) {
			parsedMessages = [];
			return;
		}
		try {
			const parsed = JSON.parse(llmConversationJson) as RawMessage[];
			parsedMessages = Array.isArray(parsed)
				? parsed.map((m) => ({
						role: m.Role as ConversationMessage['role'],
						content: m.Content ?? null,
						toolCalls: m.ToolCalls
							? m.ToolCalls.map((tc) => ({
									id: tc.Id,
									functionName: tc.FunctionName,
									arguments: tc.Arguments,
									result: tc.Result ?? null
								}))
							: null
					}))
				: [];
		} catch {
			parsedMessages = [];
		}
	}

	$effect(() => {
		if (llmConversationJson) parseConversation();
		else parsedMessages = [];
	});
</script>

{#if parsedMessages.length > 0}
	<Card.Root>
		<Card.Header>
			<Card.Title class="text-base">LLM Conversation</Card.Title>
		</Card.Header>
		<Card.Content class="space-y-3">
			{#each parsedMessages as msg, i (i)}
				<div class="rounded-lg border p-3 {msg.role === 'system' ? 'bg-muted/30' : ''}">
					<div class="flex items-center gap-2 text-sm font-medium text-muted-foreground">
						{#if msg.role === 'system'}
							<button
								onclick={() => (systemExpanded = !systemExpanded)}
								class="flex items-center gap-2 hover:text-foreground"
							>
								<ChevronRight
									class="size-3.5 transition-transform {systemExpanded ? 'rotate-90' : ''}"
								/>
								<Terminal class="size-4" />
								<span>{roleLabels[msg.role]}</span>
							</button>
						{:else if msg.role === 'user'}
							<User class="size-4" />
							<span>{roleLabels[msg.role]}</span>
						{:else if msg.role === 'assistant'}
							<Bot class="size-4" />
							<span>{roleLabels[msg.role]}</span>
						{:else}
							<Terminal class="size-4" />
							<span>{roleLabels[msg.role]}</span>
						{/if}
					</div>
					{#if msg.role === 'system'}
						{#if systemExpanded}
							<p class="mt-2 text-sm whitespace-pre-wrap">{msg.content}</p>
						{/if}
					{:else}
						{#if msg.content}
							<p class="mt-2 text-sm whitespace-pre-wrap">{msg.content}</p>
						{/if}
						{#if msg.toolCalls && msg.toolCalls.length > 0}
							<div class="mt-2 space-y-2">
								{#each msg.toolCalls as tc (tc.id)}
									<div class="rounded-md border bg-muted/20 p-2.5 text-sm">
										<div class="flex items-center gap-2 font-medium text-foreground">
											<Zap class="size-4 text-amber-500" />
											<span>{tc.functionName}</span>
										</div>
										<pre
											class="mt-1.5 overflow-x-auto rounded bg-muted/50 p-2 text-xs">{tc.arguments}</pre>
										{#if tc.result}
											<p class="mt-1 text-xs text-muted-foreground">→ {tc.result}</p>
										{/if}
									</div>
								{/each}
							</div>
						{/if}
					{/if}
				</div>
			{/each}
		</Card.Content>
	</Card.Root>
{/if}
