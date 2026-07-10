using System.Text.Json.Serialization;

namespace Hikyaku.MCP.Server.MCP;

public class InitializeResponse
{
  [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; set; } = "2025-06-18";

  [JsonPropertyName("capabilities")] public Capabilities Capabilities { get; set; } = new();

  [JsonPropertyName("serverInfo")] public ServerInfo ServerInfo { get; set; } = new();
}

public class Capabilities
{
  [JsonPropertyName("tools")] public ListChangedCapability Tools { get; set; } = new();

  [JsonPropertyName("resources")] public ListChangedCapability Resources { get; set; } = new();
}

public class ListChangedCapability
{
  [JsonPropertyName("listChanged")] public bool ListChanged { get; set; }
}
