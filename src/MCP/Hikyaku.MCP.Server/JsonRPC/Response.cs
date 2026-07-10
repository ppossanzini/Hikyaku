using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hikyaku.MCP.Server.JsonRPC;

public class Response
{
  [JsonPropertyName("jsonrpc")] public string JsonRpc { get; init; } = "2.0";

  [JsonPropertyName("id")]
  [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
  public JsonElement? Id { get; init; }

  [JsonPropertyName("result")] public object Result { get; init; }

  [JsonPropertyName("error")] public Error ErrorDetail { get; init; }

  public static Response ParseError(string message, object data = null)
  {
    return new Response { ErrorDetail = new Error(ErrorCode.ParseError) { Message = message, Data = data } };
  }

  public static Response InvalidRequest(string message, object data = null)
  {
    return new Response { ErrorDetail = new Error(ErrorCode.InvalidRequest) { Message = message, Data = data } };
  }
}
