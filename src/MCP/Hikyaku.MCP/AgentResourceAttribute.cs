namespace Hikyaku.Kaido.MCP;

/// <summary>
/// Marks an <see cref="IRequest"/> / <see cref="IRequest{TResponse}"/> class as an MCP resource.
/// Reading the resource dispatches the request through Hikyaku and returns the handler response as resource content.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AgentResourceAttribute : Attribute
{
  /// <summary>
  /// Resource name exposed via MCP. When omitted, the snake_case version of the class name is used.
  /// </summary>
  public string? Name { get; set; }

  /// <summary>
  /// Human readable title shown by MCP clients.
  /// </summary>
  public string? Title { get; set; }

  /// <summary>
  /// Description used by LLMs to decide when to read the resource.
  /// </summary>
  public string? Description { get; set; }

  /// <summary>
  /// Resource URI. When omitted, "hikyaku://resources/{name}" is used.
  /// </summary>
  public string? Uri { get; set; }

  /// <summary>
  /// MIME type of the resource content. Defaults to "application/json".
  /// </summary>
  public string MimeType { get; set; } = "application/json";
}
