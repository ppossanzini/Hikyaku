# MCP Host Example

This example demonstrates how to consume an MCP server from .NET clients using both the official Anthropic C# SDK (Claude) and OpenAI's REST API (ChatGPT).

## Running

1. Start the MCP server:
   ```bash
   ASPNETCORE_URLS=http://127.0.0.1:5310 dotnet run --project ../McpExample.Server
   ```

2. Run the host (in another terminal):
   ```bash
   dotnet run
   ```

## Environment Variables

- `MCP_ENDPOINT`: MCP server endpoint (default: `http://127.0.0.1:5310/mcp`)
- `MCP_API_KEY`: MCP server API key (default: `dev-secret-token`)
- `ANTHROPIC_API_KEY`: Required to enable Claude demo
- `OPENAI_API_KEY`: Required to enable ChatGPT demo
- `OPENAI_MODEL`: OpenAI model to use (default: `gpt-4o-mini`)

## Features

- MCP server discovery and tool listing
- Direct MCP tool invocation
- Claude (Anthropic API) agentic loop with tool use
- ChatGPT (OpenAI API) agentic loop with tool use
