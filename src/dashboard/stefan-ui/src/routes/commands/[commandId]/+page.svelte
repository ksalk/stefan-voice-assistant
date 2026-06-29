<script lang="ts">
	import { resolve } from '$app/paths';
	import { onMount } from 'svelte';
	import * as Card from '$lib/components/ui/card/index.js';
	import * as Alert from '$lib/components/ui/alert/index.js';
	import { Button } from '$lib/components/ui/button/index.js';
	import { Badge } from '$lib/components/ui/badge/index.js';
	import { Skeleton } from '$lib/components/ui/skeleton/index.js';
	import TimeAgo from '$lib/components/TimeAgo.svelte';
	import ArrowLeft from '@lucide/svelte/icons/arrow-left';
	import Mic from '@lucide/svelte/icons/mic';
	import Volume2 from '@lucide/svelte/icons/volume-2';
	import Bot from '@lucide/svelte/icons/bot';
	import User from '@lucide/svelte/icons/user';
	import Terminal from '@lucide/svelte/icons/terminal';
	import Zap from '@lucide/svelte/icons/zap';
	import ChevronRight from '@lucide/svelte/icons/chevron-right';
	import { api } from '$lib/api';
	import { formatDuration, getStatusBadgeVariant } from '$lib/commands';
	import { toast } from 'svelte-sonner';
	import type { Command, ConversationMessage } from '$lib/types';
	import type { PageProps } from './$types';

	let command = $state<Command | null>(null);
	let loading = $state(true);
	let error: string | null = $state(null);
	let showAdvanced = $state(false);
	let systemExpanded = $state(false);
	let parsedMessages = $state<ConversationMessage[]>([]);

	let { params }: PageProps = $props();
	let commandId = $derived(params.commandId);

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

	const roleLabels: Record<string, string> = {
		system: 'System prompt',
		user: 'User input',
		assistant: 'Assistant',
		tool: 'Tool result',
	};

	onMount(async () => {
		await fetchCommand(commandId);
	});

	async function fetchCommand(id: string) {
		loading = true;
		error = null;
		try {
			const cmd = await api.getCommand(id);
			command = cmd;
			parseConversation(cmd);
		} catch (e) {
			error = e instanceof Error ? e.message : 'Failed to load command details';
		} finally {
			loading = false;
		}
	}

	function parseConversation(cmd: Command) {
		if (!cmd.llmConversationJson) {
			parsedMessages = [];
			return;
		}
		try {
			const parsed = JSON.parse(cmd.llmConversationJson) as RawMessage[];
			parsedMessages = Array.isArray(parsed)
				? parsed.map((m) => ({
						role: m.Role as ConversationMessage['role'],
						content: m.Content ?? null,
						toolCalls: m.ToolCalls
							? m.ToolCalls.map((tc) => ({
									id: tc.Id,
									functionName: tc.FunctionName,
									arguments: tc.Arguments,
									result: tc.Result ?? null,
								}))
							: null,
					}))
				: [];
		} catch {
			parsedMessages = [];
		}
	}

	async function playAudio(type: 'Request' | 'Response') {
		if (!command) return;
		try {
			const blob = await api.getCommandAudio(command.id, type);
			const url = URL.createObjectURL(blob);
			const audio = new Audio(url);
			audio.onended = () => URL.revokeObjectURL(url);
			audio.onerror = () => URL.revokeObjectURL(url);
			await audio.play();
		} catch (e) {
			console.error('Failed to play audio:', e);
			toast.error('Failed to play audio: ' + (e instanceof Error ? e.message : 'Unknown error'));
		}
	}
</script>

{#if loading}
	<div class="flex items-center justify-between">
		<Skeleton class="h-8 w-40" />
		<Skeleton class="h-9 w-32" />
	</div>
	<div class="mt-4 grid gap-4 md:grid-cols-2">
		<Skeleton class="h-40 w-full" />
		<Skeleton class="h-40 w-full" />
	</div>
	<Skeleton class="mt-4 h-48 w-full" />
	<Skeleton class="mt-4 h-64 w-full" />
{:else if error}
	<Alert.Root variant="destructive" class="mt-4">
		<Alert.Title>Error</Alert.Title>
		<Alert.Description>{error}</Alert.Description>
	</Alert.Root>
	<div class="mt-4">
		<Button variant="outline" href="/commands">
			<ArrowLeft class="size-4" />
			Back to Commands
		</Button>
	</div>
{:else if command}
	<div class="flex flex-wrap items-start justify-between gap-3">
		<div>
			<h1>Command Details</h1>
			<p class="text-sm text-muted-foreground">
				From <a href={resolve(`/nodes/${command.nodeId}`)} class="font-medium hover:underline"
					>{command.nodeName}</a
				>
				· <TimeAgo date={command.receivedAt} />
			</p>
		</div>
		<Button variant="outline" size="sm" href={resolve('/commands')}>
			<ArrowLeft class="size-4" />
			Back to Commands
		</Button>
	</div>

	<div class="mt-4 flex items-center gap-3">
		<Badge variant={getStatusBadgeVariant(command.status)} class="text-sm">{command.status}</Badge>
		<span class="text-sm text-muted-foreground"
			>Total duration: {formatDuration(command.totalDurationMs)}</span
		>
	</div>

	<div class="mt-4 grid gap-4 md:grid-cols-2">
		<Card.Root>
			<Card.Header class="flex-row items-center justify-between">
				<Card.Title class="text-base">Transcript</Card.Title>
				<Button variant="outline" size="icon" aria-label="Play request audio" onclick={() => playAudio('Request')}>
					<Mic class="size-4" />
				</Button>
			</Card.Header>
			<Card.Content>
				<p class="text-sm whitespace-pre-wrap">
					{command.transcript || 'No transcript available.'}
				</p>
			</Card.Content>
		</Card.Root>

		<Card.Root>
			<Card.Header class="flex-row items-center justify-between">
				<Card.Title class="text-base">Response</Card.Title>
				<Button variant="outline" size="icon" aria-label="Play response audio" onclick={() => playAudio('Response')}>
					<Volume2 class="size-4" />
				</Button>
			</Card.Header>
			<Card.Content>
				<p class="text-sm whitespace-pre-wrap">
					{command.responseText || 'No response available.'}
				</p>
			</Card.Content>
		</Card.Root>
	</div>

	<Card.Root class="mt-4">
		<Card.Header>
			<Card.Title class="text-base">Duration Breakdown</Card.Title>
		</Card.Header>
		<Card.Content>
			<div class="grid grid-cols-2 gap-4 sm:grid-cols-4">
				<div>
					<p class="text-xs text-muted-foreground">STT</p>
					<p class="text-lg font-medium">{formatDuration(command.sttDurationMs ?? 0)}</p>
				</div>
				<div>
					<p class="text-xs text-muted-foreground">LLM</p>
					<p class="text-lg font-medium">{formatDuration(command.llmDurationMs ?? 0)}</p>
				</div>
				<div>
					<p class="text-xs text-muted-foreground">TTS</p>
					<p class="text-lg font-medium">{formatDuration(command.ttsDurationMs ?? 0)}</p>
				</div>
				<div>
					<p class="text-xs text-muted-foreground">Total</p>
					<p class="text-lg font-medium">{formatDuration(command.totalDurationMs)}</p>
				</div>
			</div>
		</Card.Content>
	</Card.Root>

	{#if parsedMessages.length > 0}
		<Card.Root class="mt-4">
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
											<pre class="mt-1.5 overflow-x-auto rounded bg-muted/50 p-2 text-xs">{tc.arguments}</pre>
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

	<div class="mt-4">
		<Button variant="ghost" size="sm" onclick={() => (showAdvanced = !showAdvanced)}>
			{showAdvanced ? 'Hide advanced' : 'Show advanced'}
		</Button>
		{#if showAdvanced}
			<div class="mt-2 grid gap-2 text-sm text-muted-foreground">
				<p><span class="font-medium">Session ID:</span> {command.sessionId}</p>
				<p><span class="font-medium">Input format:</span> {command.inputAudioFormat}</p>
				<p><span class="font-medium">Output format:</span> {command.outputAudioFormat || '—'}</p>
				{#if command.errorMessage}
					<p><span class="font-medium text-destructive">Error:</span> {command.errorMessage}</p>
				{/if}
			</div>
		{/if}
	</div>
{/if}
