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

export interface Command {
	id: string;
	nodeId: string;
	nodeName: string;
	sessionId: string;
	receivedAt: string;
	inputAudioFormat: string;
	inputAudioDurationMs: number;
	transcript: string;
	responseText: string;
	outputAudioFormat: string;
	sttDurationMs: number;
	llmDurationMs: number;
	ttsDurationMs: number;
	totalDurationMs: number;
	status: string;
	errorMessage: string | null;
}

export interface CommandsResult {
	items: Command[];
	totalCount: number;
	page: number;
	pageSize: number;
}
