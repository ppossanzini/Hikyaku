using System.Text.Json;

namespace McpExample.Host;

public record McpToolInfo(string Name, string? Title, string? Description, JsonElement InputSchema);

public class McpClient
{
  private readonly string _endpoint;
  private readonly HttpClient _client = new();
  private int _requestId = 0;

  public McpClient(string endpoint, string apiKey)
  {
    _endpoint = endpoint;
    _client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

    // Optionally: add User bearer token for Authorization header
    // _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    // server-side you can use mcp.Security.RequireAuthenticatedUser(); to validate it.

    // server side you can use IRequestEnrich<T> to read the HttpContext and extract claims, headers, etc. for each request.
    // and give specific request types access to the caller context, without coupling the handlers to HTTP.
  }

  public async Task<JsonElement> RpcAsync(string method, object? @params)
  {
    var requestId = ++_requestId;
    var request = new
    {
      jsonrpc = "2.0",
      id = requestId,
      method = method,
      @params = @params
    };

    var content = new StringContent(
      JsonSerializer.Serialize(request),
      System.Text.Encoding.UTF8,
      "application/json"
    );

    var response = await _client.PostAsync(_endpoint, content);
    response.EnsureSuccessStatusCode();

    var body = await response.Content.ReadAsStringAsync();
    var result = JsonSerializer.Deserialize<JsonElement>(body);

    if (result.TryGetProperty("error", out var error))
    {
      var message = error.TryGetProperty("message", out var msg)
        ? msg.GetString() ?? "Unknown error"
        : "Unknown error";
      throw new InvalidOperationException(message);
    }

    return result.TryGetProperty("result", out var res) ? res.Clone() : default;
  }

  public async Task<JsonElement> InitializeAsync()
  {
    var @params = new
    {
      protocolVersion = "2025-06-18",
      capabilities = new { },
      clientInfo = new { name = "mcp-host-example", version = "1.0" }
    };

    return await RpcAsync("initialize", @params);
  }

  public async Task<IReadOnlyList<McpToolInfo>> ListToolsAsync()
  {
    var result = await RpcAsync("tools/list", null);
    var tools = new List<McpToolInfo>();

    if (result.TryGetProperty("tools", out var toolsArray))
    {
      foreach (var tool in toolsArray.EnumerateArray())
      {
        var name = tool.GetProperty("name").GetString() ?? "";
        var title = tool.TryGetProperty("title", out var t) ? t.GetString() : null;
        var description = tool.TryGetProperty("description", out var d) ? d.GetString() : null;
        var inputSchema = tool.TryGetProperty("inputSchema", out var s) ? s : default;

        tools.Add(new McpToolInfo(name, title, description, inputSchema));
      }
    }

    return tools;
  }

  public async Task<(string Text, bool IsError)> CallToolAsync(string name, JsonElement? arguments)
  {
    var @params = new { name = name, arguments = arguments };
    var result = await RpcAsync("tools/call", @params);

    var text = "";
    if (result.TryGetProperty("content", out var content))
    {
      foreach (var item in content.EnumerateArray())
      {
        if (item.TryGetProperty("text", out var textElement))
        {
          text = textElement.GetString() ?? "";
          break;
        }
      }
    }

    var isError = result.TryGetProperty("isError", out var errorProp) && errorProp.GetBoolean();
    return (text, isError);
  }
}
