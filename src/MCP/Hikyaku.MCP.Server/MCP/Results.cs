using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hikyaku.MCP.Server.MCP;

public class ToolCallResult
{
  [JsonPropertyName("content")] public List<TextContent> Content { get; set; } = new();

  [JsonPropertyName("structuredContent")] public JsonElement? StructuredContent { get; set; }

  [JsonPropertyName("isError")] public bool IsError { get; set; }
}

public class TextContent
{
  [JsonPropertyName("type")] public string Type { get; init; } = "text";

  [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
}

public class ResourceReadResult
{
  [JsonPropertyName("contents")] public List<ResourceContent> Contents { get; set; } = new();
}

public class ResourceContent
{
  [JsonPropertyName("uri")] public string Uri { get; set; } = string.Empty;

  [JsonPropertyName("mimeType")] public string MimeType { get; set; } = "application/json";

  [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
}

public class ToolsListResult
{
  [JsonPropertyName("tools")] public List<ToolListItem> Tools { get; set; } = new();
}

public class ToolListItem
{
  [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

  [JsonPropertyName("title")] public string Title { get; set; }

  [JsonPropertyName("description")] public string Description { get; set; }

  [JsonPropertyName("inputSchema")] public object InputSchema { get; set; } = new();
}

public class ResourcesListResult
{
  [JsonPropertyName("resources")] public List<ResourceListItem> Resources { get; set; } = new();
}

public class ResourceListItem
{
  [JsonPropertyName("uri")] public string Uri { get; set; } = string.Empty;

  [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

  [JsonPropertyName("title")] public string Title { get; set; }

  [JsonPropertyName("description")] public string Description { get; set; }

  [JsonPropertyName("mimeType")] public string MimeType { get; set; }
}
