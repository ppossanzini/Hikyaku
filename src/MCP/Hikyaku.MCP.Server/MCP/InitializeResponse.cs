namespace Hikyaku.Kaido.MCP.Server.MCP;

public class InitializeResponse
{
  public string ProtocolVersion { get; set; } = "2025-06-18";
  public object Capabilities { get; set; } = new Capabilities();
  public ServerInfo ServerInfo { get; set; }

  public string protocolVersion => ProtocolVersion;
  public object capabilities => Capabilities;
  public ServerInfo serverInfo => ServerInfo;
}

public class Capabilities
{
  public object Tools { get; set; } = new();
  public object Resources { get; set; } = new();

  public object tools => Tools;
  public object resources => Resources;
}