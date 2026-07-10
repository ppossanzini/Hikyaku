using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hikyaku.Kaido.MCP.Server.JsonRPC;

public class Request
{
  [JsonPropertyName("jsonrpc")] public string JsonRpc { get; init; } = "2.0";

  [JsonPropertyName("method")] public string Method { get; set; } = string.Empty;

  [JsonPropertyName("id")] public JsonElement? Id { get; set; }

  [JsonPropertyName("params")] public JsonElement? Params { get; set; }

  [JsonIgnore]
  public bool IsNotification => Id is null || Id.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;

  public Response SuccessfulResponse(object result)
  {
    return new Response { Id = Id, Result = result };
  }

  public Response ErrorResponse(ErrorCode code, string message, object? errorData = null)
  {
    return new Response { Id = Id, ErrorDetail = new Error(code) { Message = message, Data = errorData } };
  }
}
