using System.Text.Json;
using System.Text.Json.Nodes;

namespace McpExample.Host;

public static class OpenAIAgent
{
  public static async Task RunAsync(McpClient mcp, IReadOnlyList<McpToolInfo> mcpTools, string prompt)
  {
    var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
    var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";
    var client = new HttpClient();
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

    var tools = new JsonArray();
    foreach (var mcpTool in mcpTools)
    {
      tools.Add(new JsonObject
      {
        ["type"] = "function",
        ["function"] = new JsonObject
        {
          ["name"] = mcpTool.Name,
          ["description"] = mcpTool.Description ?? "",
          ["parameters"] = JsonNode.Parse(mcpTool.InputSchema.GetRawText())
        }
      });
    }

    var messages = new JsonArray
    {
      new JsonObject
      {
        ["role"] = "user",
        ["content"] = prompt
      }
    };

    while (true)
    {
      var request = new JsonObject
      {
        ["model"] = model,
        ["messages"] = messages,
        ["tools"] = tools
      };

      var content = new StringContent(
        request.ToJsonString(),
        System.Text.Encoding.UTF8,
        "application/json"
      );

      var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);

      if (!response.IsSuccessStatusCode)
      {
        var body = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[chatgpt] Error {response.StatusCode}: {body}");
        return;
      }

      var bodyText = await response.Content.ReadAsStringAsync();
      var result = JsonNode.Parse(bodyText);

      var choice = result?["choices"]?[0]?["message"];
      if (choice == null)
        break;

      if (choice["tool_calls"] is JsonArray toolCallsArray && toolCallsArray.Count > 0)
      {
        var assistantMessage = new JsonObject { ["role"] = "assistant" };
        var toolCallsCopy = new JsonArray();
        foreach (var tc in toolCallsArray)
        {
          toolCallsCopy.Add(tc?.DeepClone());
        }
        assistantMessage["tool_calls"] = toolCallsCopy;
        messages.Add(assistantMessage);

        foreach (var tc in toolCallsArray)
        {
          if (tc == null) continue;
          var toolUseId = tc["id"]?.GetValue<string>() ?? "";
          var toolName = tc["function"]?["name"]?.GetValue<string>() ?? "";
          var argsText = tc["function"]?["arguments"]?.GetValue<string>() ?? "{}";
          var arguments = JsonSerializer.Deserialize<JsonElement>(argsText);

          Console.WriteLine($"[chatgpt] tool call: {toolName}({arguments})");
          var (resultText, _) = await mcp.CallToolAsync(toolName, arguments);

          messages.Add(new JsonObject
          {
            ["role"] = "tool",
            ["tool_call_id"] = toolUseId,
            ["content"] = resultText
          });
        }
      }
      else
      {
        var textContent = choice["content"]?.GetValue<string>() ?? "";
        Console.WriteLine($"[chatgpt] {textContent}");
        break;
      }
    }
  }
}
