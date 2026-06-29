<script lang="ts">
	import * as Card from '$lib/components/ui/card/index.js';
	import Bot from '@lucide/svelte/icons/bot';
	import User from '@lucide/svelte/icons/user';
	import Settings from '@lucide/svelte/icons/settings';
	import MessageSquare from '@lucide/svelte/icons/message-square';
	import Zap from '@lucide/svelte/icons/zap';
	import ChevronRight from '@lucide/svelte/icons/chevron-right';
	import type { ConversationMessage } from '$lib/types';

	let { llmConversationJson }: { llmConversationJson: string | null } = $props();

	let parsedMessages = $state<ConversationMessage[]>([]);
	let expandedMap = $state<Map<string, boolean>>(new Map());

	function toggleExpanded(idx: number) {
		const key = String(idx);
		expandedMap.set(key, !expandedMap.get(key));
		expandedMap = new Map(expandedMap);
	}

	function isExpanded(idx: number, role: string): boolean {
		const key = String(idx);
		if (!expandedMap.has(key)) {
			return role !== 'system';
		}
		return expandedMap.get(key) ?? true;
	}

	const roleLabels: Record<string, string> = {
		system: 'System prompt',
		user: 'User input',
		assistant: 'Assistant'
	};

	const roleBgClasses: Record<string, string> = {
		system: 'bg-muted/30',
		user: 'bg-blue-500/5',
		assistant: 'bg-green-500/5'
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
				<div class="rounded-lg border p-3 {roleBgClasses[msg.role] ?? ''}">
					<div class="flex items-start gap-2">
						<button
							onclick={() => toggleExpanded(i)}
							class="mt-1 shrink-0 text-muted-foreground hover:text-foreground"
						>
							<ChevronRight
								class="size-3.5 transition-transform {isExpanded(i, msg.role) ? 'rotate-90' : ''}"
							/>
						</button>
						<div class="min-w-0 flex-1">
							<div class="flex items-center gap-2 text-sm font-medium text-muted-foreground">
								{#if msg.role === 'system'}
									<Settings class="mt-0.5 size-4" />
								{:else if msg.role === 'user'}
									<User class="size-4" />
								{:else if msg.role === 'assistant'}
									<Bot class="size-4" />
								{:else}
									<MessageSquare class="mt-0.5 size-4" />
								{/if}
								<span>{roleLabels[msg.role]}</span>
							</div>
							{#if isExpanded(i, msg.role)}
								{#if msg.content}
									<p class="mt-2 text-sm whitespace-pre-wrap">{msg.content}</p>
								{/if}
								{#if msg.toolCalls && msg.toolCalls.length > 0}
									<div class="mt-2 space-y-2">
										{#each msg.toolCalls as tc (tc.id)}
											<div class="rounded-md border bg-cyan-500/10 p-2.5 text-sm">
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
					</div>
				</div>
			{/each}
		</Card.Content>
	</Card.Root>
{/if}
