using Hikyaku;
using Hikyaku.Kaido.MCP;
using McpExample.DTO;

namespace McpExample.Requests;

[AgentResource(Name = "server_metadata",
  Title = "Server metadata",
  Description = "Static information about the host running this MCP server.")]
public class ServerMetadataResource : IRequest<ServerMetadataResult>
{
  public string? ClientName { get; set; }
}
