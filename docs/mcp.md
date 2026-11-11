# Production data in OpenCode via MCP

The `stefan` MCP server gives OpenCode read-only access to a running Stefan server. It runs locally over stdio and calls the existing HTTP API. It does not change the server, the dashboard, or the database.

## Architecture

```text
OpenCode
  └─ stdio MCP
       └─ src/mcp/Stefan.Mcp (local .NET process)
            └─ HTTP GET → Stefan server /api
```

## Prerequisites

- .NET 10 SDK, installed by `mise install`.
- Network access to the Stefan server.
- A server URL. Put it in `.env.mcp` at the repository root, or export `STEFAN_API_URL` in your shell. Do not commit the real URL.

## Setup

Create a local, gitignored `.env.mcp` file with the server origin:

```bash
cat > .env.mcp <<'EOF'
STEFAN_API_URL=http://stefan.example:5285
EOF
```

Replace `http://stefan.example:5285` with the real origin. The file is ignored by Git. If you prefer, export `STEFAN_API_URL` in the shell instead.

Build the MCP server:

```bash
mise run build-mcp
```

Check the OpenCode connection:

```bash
opencode mcp list
```

The output shows `stefan  connected` when the server is ready.

The project configuration lives in `opencode.jsonc` at the repository root:

```jsonc
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "servers": {
      "stefan": {
        "type": "local",
        "command": ["bash", "scripts/run-stefan-mcp.sh"],
        "timeout": {
          "startup": 60000
        }
      }
    }
  }
}
```

The wrapper reads `STEFAN_API_URL` from `.env.mcp` when the variable is not already set. The real URL stays local and is not committed.

OpenCode reloads configuration automatically. No restart is needed.

## Configuration

| Variable | Required | Purpose |
| --- | --- | --- |
| `STEFAN_API_URL` | Yes | Server origin, for example `http://stefan.example:5285`. Read from the environment or `.env.mcp`. The client also accepts an origin plus `/api`, or a copied endpoint URL such as `/api/nodes`. |
| `STEFAN_API_TOKEN` | No | Sends `Authorization: Bearer <token>`. The current server does not validate it. Use it when server authentication is added. |

Do not put tokens in `opencode.jsonc`. Use `{env:STEFAN_API_TOKEN}` instead.

## Tools

| Tool | Description |
| --- | --- |
| `health` | Server health, version, commit, STT/TTS providers, and LLM model. |
| `list_nodes` | Registered nodes with status, last seen time, IP, port, restart count, and detector. |
| `get_node` | One node with recent CPU, memory, disk, audio volume, version, and commit reports. Newest first, limited to 20 by default. |
| `list_commands` | Voice commands, newest first. Supports pagination, node filter, and status filter. |
| `get_command` | One command with transcript, response, timings, status, and error. |
| `get_command_tools` | Tool documents changed by one command, with action and payload. |
| `get_command_logs` | Loki log entries for one command. Requires Loki on the server. |
| `list_tool_documents` | Live or archived tool documents, such as shopping lists and timers. |
| `get_tool_document` | One tool document with payload and command history. |

All tools are read-only. The MCP server has no code path for `POST` or `DELETE`.

### Limits

- `list_commands` defaults to `pageSize=10`. The maximum is 100.
- `list_tool_documents` defaults to `pageSize=10`. The maximum is 100.
- `get_node` defaults to 20 status reports and caps at 200. The response includes `returnedStatusReports`, `totalStatusReports`, and `statusReportsTruncated`.
- `get_command_logs` defaults to 50 entries. The maximum is 500. The response includes `returnedEntries`, `truncated`, and `maxEntries`.
- `get_command` removes `llmConversationJson` unless `includeLlmConversation=true`.

## Use in OpenCode

Code Mode is enabled by default. Call the tools through the `tools.stefan` namespace:

```js
tools.stefan.health()
tools.stefan.list_commands({ page: 1, pageSize: 5 })
tools.stefan.get_command({ commandId: "<id>" })
```

To expose the tools as native tool entries instead, set `"codemode": false` on the MCP server.

## Security

- The MCP server is read-only.
- Keep the Stefan server on the private network. Do not forward port 5285.
- The dashboard API currently accepts every request. Network reachability is the only control. Fix dashboard authentication before exposing the API outside the LAN.
- Command transcripts and logs are sent to the OpenCode model provider. Use a provider you trust.
- The MCP process inherits OpenCode's environment. Do not export secrets that the server does not need.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| `stefan` is not connected | Run `bash scripts/run-stefan-mcp.sh` manually and read stderr. |
| `STEFAN_API_URL is required` | Create `.env.mcp` or export `STEFAN_API_URL` before starting OpenCode. |
| Connection refused | Check that the server is running and reachable from this machine. |
| Request timed out | Check the server and the 20-second client timeout. |
| TLS error | The current setup uses plain HTTP. If you switch to HTTPS with a private CA, install the CA on this machine. |
| Logs tool reports an external error | Loki is not configured or is unreachable on the server. |
| Missing tools | Run `/mcps` in OpenCode and check the server state. |

## Tests

The integration tests start a fake Stefan API and the real MCP server over stdio. They check the tool list, query forwarding, log truncation, and error mapping.

```bash
mise run test-mcp
```
