using System.Text.Json.Serialization;

namespace Hikyaku.Kaido.MCP.Server.JsonRPC;

public class Error(ErrorCode code)
{
  [JsonPropertyName("code")] public int Code { get; set; } = (int)code;

  [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;

  [JsonPropertyName("data")] public object Data { get; set; }
}

public enum ErrorCode
{
  ParseError = -32700,
  InvalidRequest = -32600,
  MethodNotFound = -32601,
  InvalidParams = -32602,
  InternalError = -32603,
  ResourceNotFound = -32002,
  Unauthorized = -32001
}
