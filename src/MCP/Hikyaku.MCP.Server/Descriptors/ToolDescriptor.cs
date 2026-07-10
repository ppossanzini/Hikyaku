namespace Hikyaku.MCP.Server.Descriptors;

public class ToolDescriptor
{
  public required string Name { get; init; }
  public string Title { get; init; }
  public string Description { get; init; }
  public required Type RequestType { get; init; }
  public required object InputSchema { get; init; }
}
