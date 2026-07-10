using System;

namespace Hikyaku.MCP
{

  /// <summary>
  /// Marks an <see cref="IRequest"/> / <see cref="IRequest{TResponse}"/> class as an MCP tool.
  /// The request properties become the tool input schema and the handler response becomes the tool result.
  /// </summary>
  [AttributeUsage(AttributeTargets.Class, Inherited = false)]
  public sealed class AgentToolAttribute : Attribute
  {
    /// <summary>
    /// Tool name exposed via MCP. When omitted, the snake_case version of the class name is used.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Human readable title shown by MCP clients.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Description used by LLMs to decide when to invoke the tool.
    /// </summary>
    public string Description { get; set; }
  }
}
