using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace McpExample.Host;

public static class ClaudeAgent
{
  public static async Task RunAsync(McpClient mcp, IReadOnlyList<McpToolInfo> mcpTools, string prompt)
  {
    var client = new AnthropicClient();

    var tools = new List<Tool>();
    foreach (var mcpTool in mcpTools)
    {
      var properties = new Dictionary<string, JsonElement>();
      var required = Array.Empty<string>();

      if (mcpTool.InputSchema.TryGetProperty("properties", out var propsElement))
      {
        foreach (var prop in propsElement.EnumerateObject())
        {
          properties[prop.Name] = prop.Value.Clone();
        }
      }

      if (mcpTool.InputSchema.TryGetProperty("required", out var reqElement))
      {
        var reqs = new List<string>();
        foreach (var item in reqElement.EnumerateArray())
        {
          if (item.GetString() is string s)
            reqs.Add(s);
        }
        required = reqs.ToArray();
      }

      tools.Add(new Tool
      {
        Name = mcpTool.Name,
        Description = mcpTool.Description ?? "",
        InputSchema = new() { Properties = properties, Required = required }
      });
    }

    var messages = new List<MessageParam> { new() { Role = Role.User, Content = prompt } };

    while (true)
    {
      var response = await client.Messages.Create(new MessageCreateParams
      {
        Model = "claude-opus-4-8",
        MaxTokens = 1024,
        Tools = [..tools],
        Messages = messages,
      });

      List<ContentBlockParam> assistantContent = [];
      List<ContentBlockParam> toolResults = [];

      foreach (ContentBlock block in response.Content)
      {
        if (block.TryPickText(out TextBlock? text))
        {
          Console.WriteLine($"[claude] {text.Text}");
          assistantContent.Add(new TextBlockParam { Text = text.Text });
        }
        else if (block.TryPickToolUse(out ToolUseBlock? toolUse))
        {
          assistantContent.Add(new ToolUseBlockParam { ID = toolUse.ID, Name = toolUse.Name, Input = toolUse.Input });
          var arguments = JsonSerializer.SerializeToElement(toolUse.Input);
          Console.WriteLine($"[claude] tool call: {toolUse.Name}({arguments})");
          var (resultText, isError) = await mcp.CallToolAsync(toolUse.Name, arguments);
          toolResults.Add(new ToolResultBlockParam { ToolUseID = toolUse.ID, Content = resultText });
        }
      }

      messages.Add(new MessageParam { Role = Role.Assistant, Content = assistantContent });
      if (toolResults.Count == 0) break;
      messages.Add(new MessageParam { Role = Role.User, Content = toolResults });
    }
  }
}
