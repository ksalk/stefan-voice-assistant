export type NodeStatus = 'Online' | 'Offline';

export interface NodeStatusReport {
	timestamp: string;
	status: NodeStatus;
	cpuUsage: number | null;
	memoryUsage: number | null;
	diskUsage: number | null;
	audioVolume: number | null;
	version: string | null;
	gitCommit: string | null;
}

export interface NodeSummary {
	id: string;
	name: string;
	currentSessionId: string;
	lastKnownIpAddress: string;
	port: number;
	status: NodeStatus;
	registeredAt: string;
	lastSeenAt: string | null;
	lastPingAt: string | null;
	restartCount: number;
}

export interface Node extends NodeSummary {
	statusReports: NodeStatusReport[];
}

export interface GetNodesResult {
	nodes: NodeSummary[];
}

export interface GetNodeDetailsResult {
	node: Node;
}
