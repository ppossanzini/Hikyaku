namespace Hikyaku.MCP.Server.Descriptors;

public class ResourceDescriptor
{
  public required string Name { get; init; }
  public string Title { get; init; }
  public string Description { get; init; }
  public required string Uri { get; init; }
  public required string MimeType { get; init; }
  public required Type RequestType { get; init; }
}

