<script lang="ts">
	import * as Table from '$lib/components/ui/table';
	import TimeAgo from '$lib/components/TimeAgo.svelte';
	import { formatDateTime } from '$lib/utils/date';
	import type { PageProps } from './$types';

	let { data }: PageProps = $props();
</script>

<h1>Nodes</h1>

<Table.Root class="mt-4">
	<Table.Caption>Nodes registered in the system.</Table.Caption>
	<Table.Header>
		<Table.Row>
			<Table.Head class="w-[100px]">Name</Table.Head>
			<Table.Head>Status</Table.Head>
			<Table.Head>IP</Table.Head>
			<Table.Head>Registered</Table.Head>
			<Table.Head>Last Ping</Table.Head>
			<Table.Head>Ping Now</Table.Head>
		</Table.Row>
	</Table.Header>
	<Table.Body>
		{#each data.nodes as node (node.id)}
			<Table.Row>
				<Table.Cell class="font-medium">{node.name}</Table.Cell>
				<Table.Cell>{node.status}</Table.Cell>
				<Table.Cell>{node.lastKnownIpAddress}:{node.port}</Table.Cell>
				<Table.Cell>{formatDateTime(node.registeredAt)}</Table.Cell>
				<Table.Cell><TimeAgo date={node.lastPingAt} /></Table.Cell>
				<Table.Cell></Table.Cell>
			</Table.Row>
		{/each}
	</Table.Body>
</Table.Root>
