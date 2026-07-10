using System.Text.Json.Serialization;

namespace Hikyaku.MCP.Server;

public class ServerInfo
{
  [JsonPropertyName("name")] public string Name { get; set; } = "Hikyaku MCP Server";

  [JsonPropertyName("title")] public string Title { get; set; }

  [JsonPropertyName("version")] public string Version { get; set; } = "1.0.0";
}
