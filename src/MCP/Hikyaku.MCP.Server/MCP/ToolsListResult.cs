using System.Text.Json.Serialization;

namespace Hikyaku.Kaido.MCP.Server.dto;

public class ToolsListResult
{
  [JsonPropertyName("tools")]
  public List<McpTool> Tools { get; set; } = new();
}