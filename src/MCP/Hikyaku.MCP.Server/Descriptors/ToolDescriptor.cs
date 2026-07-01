namespace Hikyaku.Kaido.MCP.Server.Descriptors;

public class ToolDescriptor
{
  public string Name { get; set; }
  public string Title { get; set; }
  public string Descriptor { get; set; }
  public Type KaidoType { get; set; }
  public string[] Required { get; set; }
  public Type ReturnType { get; set; }
  public PropertyDescriptor[] Properties { get; set; }
}