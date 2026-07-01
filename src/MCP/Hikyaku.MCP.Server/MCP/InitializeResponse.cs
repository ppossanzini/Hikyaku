namespace Hikyaku.Kaido.MCP.Server.MCP;

public class InitializeResponse
{
  public string ProtocolVersion { get; set; } = "2025-06-18";
  public object Capabilities { get; set; } = new Capabilities();
  public ServerInfo ServerInfo { get; set; }
}

public class Capabilities
{
  public object Tools { get; set; } = new();
  protected object Resources { get; set; } = new();
}