using System.ComponentModel;
using Hikyaku;
using Hikyaku.Kaido.MCP;
using McpExample.DTO;

namespace McpExample.Requests;

[AgentTool(Name = "get_server_time",
  Title = "Get server time",
  Description = "Returns the current UTC time of the server, optionally annotated with a time zone hint.")]
public class GetServerTimeTool : IRequest<GetServerTimeResult>
{
  [Description("Optional IANA time zone identifier used to annotate the response (e.g. Europe/Rome).")]
  public string? TimeZoneHint { get; set; }
}
