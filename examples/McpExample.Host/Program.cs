using System.Text.Json;

var mcpEndpoint = Environment.GetEnvironmentVariable("MCP_ENDPOINT") ?? "http://127.0.0.1:5310/mcp";
var mcpApiKey = Environment.GetEnvironmentVariable("MCP_API_KEY") ?? "dev-secret-token";

var mcp = new McpExample.Host.McpClient(mcpEndpoint, mcpApiKey);

Console.WriteLine("=== MCP discovery ===");
try
{
  var serverInfo = await mcp.InitializeAsync();
  if (serverInfo.TryGetProperty("serverInfo", out var info))
  {
    var name = info.TryGetProperty("name", out var n) ? n.GetString() : "unknown";
    var version = info.TryGetProperty("version", out var v) ? v.GetString() : "unknown";
    Console.WriteLine($"Server: {name} v{version}");
  }

  var tools = await mcp.ListToolsAsync();
  Console.WriteLine($"Found {tools.Count} tools:");
  foreach (var tool in tools)
  {
    var desc = tool.Description ?? "(no description)";
    Console.WriteLine($"  - {tool.Name}: {desc}");
  }

  Console.WriteLine("\n=== Direct tool call ===");
  var arguments = JsonSerializer.SerializeToElement(new { a = 19, b = 23 });
  var (text, isError) = await mcp.CallToolAsync("sum_numbers", arguments);
  Console.WriteLine($"sum_numbers(19, 23) = {text}");

  var prompt = "What is 19 + 23? Use the available tools to compute it, then tell me the current server time.";

  var anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
  if (!string.IsNullOrEmpty(anthropicKey))
  {
    Console.WriteLine("\n=== Claude (Anthropic API) ===");
    try
    {
      await McpExample.Host.ClaudeAgent.RunAsync(mcp, tools, prompt);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error: {ex.Message}");
    }
  }
  else
  {
    Console.WriteLine("\n=== Claude (Anthropic API) ===");
    Console.WriteLine("ANTHROPIC_API_KEY not set - skipping Claude demo.");
  }

  var openaiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
  if (!string.IsNullOrEmpty(openaiKey))
  {
    Console.WriteLine("\n=== ChatGPT (OpenAI API) ===");
    try
    {
      await McpExample.Host.OpenAIAgent.RunAsync(mcp, tools, prompt);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error: {ex.Message}");
    }
  }
  else
  {
    Console.WriteLine("\n=== ChatGPT (OpenAI API) ===");
    Console.WriteLine("OPENAI_API_KEY not set - skipping ChatGPT demo.");
  }
}
catch (Exception ex)
{
  Console.WriteLine($"Error: {ex.Message}");
  Environment.Exit(1);
}
