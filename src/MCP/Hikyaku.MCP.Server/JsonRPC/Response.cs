using System.Text.Json.Serialization;
using Hikyaku.Kaido.MCP.Server.dto;

namespace Hikyaku.Kaido.MCP.Server.JsonRPC;

public class Response(object result, object id)
{
  [JsonPropertyName("jsonrpc")] public string JsonRpc { get; init; } = "2.0";

  [JsonPropertyName("result")] public object Result { get; init; } = result;

  [JsonPropertyName("id")] public object Id { get; init; } = id;

  [JsonPropertyName("error")] public Error ErrorDetail { get; init; }

  public static Response Error(ErrorCode code, string message, object data = null)
  {
    return new Response(null, null) { ErrorDetail = new Error(code) { Message = message, Data = data } };
  }

  public static Response ParseError(string message, object data = null) => Error(ErrorCode.ParseError, message, data);
  public static Response InvalidRequest(string message, object data = null) => Error(ErrorCode.InvalidRequest, message, data);
  public static Response MethodNotFound(string message, object data = null) => Error(ErrorCode.MethodNotFound, message, data);
  public static Response InvalidParams(string message, object data = null) => Error(ErrorCode.InvalidParams, message, data);
  public static Response InternalError(string message, object data = null) => Error(ErrorCode.InternalError, message, data);
}