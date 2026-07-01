namespace Hikyaku.Kaido.MCP.Server.dto;

using System.Text.Json.Serialization;

public class McpTool
{
  [JsonPropertyName("name")]
  public string Name { get; set; } = string.Empty;

  [JsonPropertyName("description")]
  public string Description { get; set; } = string.Empty;

  [JsonPropertyName("inputSchema")]
  public object InputSchema { get; set; } = new();
}
