#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Load the server URL from a local, gitignored file when it is not already set.
if [[ -z "${STEFAN_API_URL:-}" && -f "$root/.env.mcp" ]]; then
  STEFAN_API_URL="$(sed -n 's/^STEFAN_API_URL=//p' "$root/.env.mcp" | tail -n 1)"
  export STEFAN_API_URL
fi

# Build output must go to stderr. stdout belongs to the MCP stdio protocol.
dotnet build "$root/src/mcp/Stefan.Mcp/Stefan.Mcp.csproj" -c Release --nologo -v quiet 1>&2

exec dotnet "$root/src/mcp/Stefan.Mcp/bin/Release/net10.0/Stefan.Mcp.dll"
