namespace Hikyaku.Kaido.MCP.Server.dto;

public class Error(ErrorCode? code)
{
  public int Code { get; set; } = (int)code.Value;
  public string Message { get; set; }
  public object Data { get; set; }
}

public enum ErrorCode : int
{
  ParseError = -32700,
  InvalidRequest = -32600,
  MethodNotFound = -32601,
  InvalidParams = -32602,
  InternalError = -32603
}