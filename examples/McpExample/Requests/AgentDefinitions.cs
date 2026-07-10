using System.ComponentModel;
using Hikyaku;
using Hikyaku.Kaido.MCP;

namespace McpExample;

[AgentTool(Name = "get_server_time",
  Title = "Get server time",
  Description = "Returns the current UTC time of the server, optionally annotated with a time zone hint.")]
public class GetServerTimeTool : IRequest<GetServerTimeResult>
{
  [Description("Optional IANA time zone identifier used to annotate the response (e.g. Europe/Rome).")]
  public string? TimeZoneHint { get; set; }
}

public class GetServerTimeResult
{
  public DateTime UtcNow { get; set; }
  public string? TimeZoneHint { get; set; }
}

[AgentTool(Name = "sum_numbers",
  Title = "Sum two numbers",
  Description = "Adds two integers and returns the sum. Useful to verify tool invocation end to end.")]
public class SumNumbersTool : IRequest<SumNumbersResult>
{
  [Description("First addend.")]
  public int A { get; set; }

  [Description("Second addend.")]
  public int B { get; set; }
}

public class SumNumbersResult
{
  public int Sum { get; set; }
}

[AgentResource(Name = "server_metadata",
  Title = "Server metadata",
  Description = "Static information about the host running this MCP server.")]
public class ServerMetadataResource : IRequest<ServerMetadataResult>
{
  public string? ClientName { get; set; }
}

public class ServerMetadataResult
{
  public string Environment { get; set; } = string.Empty;
  public string Machine { get; set; } = string.Empty;
  public string? ClientName { get; set; }
}
