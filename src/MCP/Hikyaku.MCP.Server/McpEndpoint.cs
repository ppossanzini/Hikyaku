using System.Text.Json;
using System.Text.Json.Serialization;
using Hikyaku.MCP.Server.Descriptors;
using Hikyaku.MCP.Server.JsonRPC;
using Hikyaku.MCP.Server.MCP;
using Hikyaku.MCP.Server.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hikyaku.MCP.Server;

/// <summary>
/// Handles MCP JSON-RPC requests over the streamable HTTP transport (single POST endpoint).
/// </summary>
internal static class McpEndpoint
{
  internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
  {
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
  };

  internal static async Task HandleAsync(HttpContext context)
  {
    Request request = null;
    try
    {
      request = await JsonSerializer.DeserializeAsync<Request>(context.Request.Body, SerializerOptions, context.RequestAborted);
    }
    catch (JsonException)
    {
      // Handled below as a parse error.
    }

    if (request is null)
    {
      await WriteAsync(context, Response.ParseError("Request body is not a valid JSON-RPC message."));
      return;
    }

    if (string.IsNullOrWhiteSpace(request.Method))
    {
      await WriteAsync(context, Response.InvalidRequest("Missing JSON-RPC method."));
      return;
    }

    var callContext = new McpCallContext
    {
      HttpContext = context,
      Method = request.Method,
      ComponentName = GetStringProperty(request.Params, "name") ?? GetStringProperty(request.Params, "uri")
    };

    var options = context.RequestServices.GetRequiredService<McpServerOptions>();
    var guardResult = await McpSecurityPipeline.EvaluateAsync(callContext, options.Security, context.RequestAborted);
    if (!guardResult.Allowed)
    {
      context.Response.StatusCode = StatusCodes.Status401Unauthorized;
      context.Response.Headers.WWWAuthenticate = "Bearer";
      await WriteAsync(context, request.ErrorResponse(ErrorCode.Unauthorized, guardResult.Reason ?? "Unauthorized."));
      return;
    }

    // Notifications (no id) expect no response body.
    if (request.IsNotification)
    {
      context.Response.StatusCode = StatusCodes.Status202Accepted;
      return;
    }

    var response = await DispatchAsync(context, request, callContext);
    await WriteAsync(context, response);
  }

  private static async Task<Response> DispatchAsync(HttpContext context, Request request, McpCallContext callContext)
  {
    var registry = context.RequestServices.GetRequiredService<McpRegistry>();
    var options = context.RequestServices.GetRequiredService<McpServerOptions>();

    switch (request.Method)
    {
      case "initialize":
        return request.SuccessfulResponse(new InitializeResponse { ServerInfo = options.ServerInfo });

      case "ping":
        return request.SuccessfulResponse(new { });

      case "tools/list":
        return request.SuccessfulResponse(new ToolsListResult
        {
          Tools = registry.Tools.Select(tool => new ToolListItem
          {
            Name = tool.Name,
            Title = tool.Title,
            Description = tool.Description,
            InputSchema = tool.InputSchema
          }).ToList()
        });

      case "resources/list":
        return request.SuccessfulResponse(new ResourcesListResult
        {
          Resources = registry.Resources.Select(resource => new ResourceListItem
          {
            Uri = resource.Uri,
            Name = resource.Name,
            Title = resource.Title,
            Description = resource.Description,
            MimeType = resource.MimeType
          }).ToList()
        });

      case "tools/call":
        return await ToolsCallAsync(context, request, registry, callContext);

      case "resources/read":
        return await ResourcesReadAsync(context, request, registry, callContext);

      default:
        return request.ErrorResponse(ErrorCode.MethodNotFound, $"Method '{request.Method}' is not supported.");
    }
  }

  private static async Task<Response> ToolsCallAsync(HttpContext context, Request request, McpRegistry registry, McpCallContext callContext)
  {
    var name = GetStringProperty(request.Params, "name");
    if (string.IsNullOrWhiteSpace(name))
    {
      return request.ErrorResponse(ErrorCode.InvalidParams, "Missing tool name in tools/call params.");
    }

    if (!registry.TryGetTool(name, out var tool))
    {
      return request.ErrorResponse(ErrorCode.InvalidParams, $"Tool '{name}' was not found.");
    }

    object instance;
    try
    {
      instance = Materialize(tool.RequestType, GetProperty(request.Params, "arguments"));
    }
    catch (JsonException exception)
    {
      return request.ErrorResponse(ErrorCode.InvalidParams, $"Invalid arguments for tool '{name}': {exception.Message}");
    }

    try
    {
      await McpSecurityPipeline.EnrichAsync(instance, callContext, context.RequestAborted);

      var hikyaku = context.RequestServices.GetRequiredService<IHikyaku>();
      var result = await hikyaku.SendObject(instance, context.RequestAborted);
      var element = result is null ? (JsonElement?)null : JsonSerializer.SerializeToElement(result, result.GetType(), SerializerOptions);

      return request.SuccessfulResponse(new ToolCallResult
      {
        Content = { new TextContent { Text = ToText(element) } },
        StructuredContent = element is { ValueKind: JsonValueKind.Object } ? element : null,
        IsError = false
      });
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception exception)
    {
      return request.SuccessfulResponse(new ToolCallResult
      {
        Content = { new TextContent { Text = exception.Message } },
        IsError = true
      });
    }
  }

  private static async Task<Response> ResourcesReadAsync(HttpContext context, Request request, McpRegistry registry, McpCallContext callContext)
  {
    var uri = GetStringProperty(request.Params, "uri");
    if (string.IsNullOrWhiteSpace(uri))
    {
      return request.ErrorResponse(ErrorCode.InvalidParams, "Missing resource uri in resources/read params.");
    }

    if (!registry.TryGetResource(uri, out var resource))
    {
      return request.ErrorResponse(ErrorCode.ResourceNotFound, $"Resource '{uri}' was not found.");
    }

    object instance;
    try
    {
      instance = Materialize(resource.RequestType, GetProperty(request.Params, "arguments"));
    }
    catch (JsonException exception)
    {
      return request.ErrorResponse(ErrorCode.InvalidParams, $"Invalid arguments for resource '{uri}': {exception.Message}");
    }

    try
    {
      await McpSecurityPipeline.EnrichAsync(instance, callContext, context.RequestAborted);

      var hikyaku = context.RequestServices.GetRequiredService<IHikyaku>();
      var result = await hikyaku.SendObject(instance, context.RequestAborted);
      var element = result is null ? (JsonElement?)null : JsonSerializer.SerializeToElement(result, result.GetType(), SerializerOptions);

      return request.SuccessfulResponse(new ResourceReadResult
      {
        Contents =
        {
          new ResourceContent
          {
            Uri = resource.Uri,
            MimeType = resource.MimeType,
            Text = ToText(element)
          }
        }
      });
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception exception)
    {
      return request.ErrorResponse(ErrorCode.InternalError, exception.Message);
    }
  }

  private static object Materialize(Type requestType, JsonElement? arguments)
  {
    var json = arguments is { ValueKind: not (JsonValueKind.Undefined or JsonValueKind.Null) } element
      ? element.GetRawText()
      : "{}";

    return JsonSerializer.Deserialize(json, requestType, SerializerOptions)
           ?? throw new JsonException($"Arguments could not be deserialized into '{requestType.Name}'.");
  }

  private static string ToText(JsonElement? element)
  {
    if (element is null)
    {
      return string.Empty;
    }

    return element.Value.ValueKind == JsonValueKind.String
      ? element.Value.GetString() ?? string.Empty
      : element.Value.GetRawText();
  }

  private static JsonElement? GetProperty(JsonElement? parameters, string propertyName)
  {
    if (parameters is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty(propertyName, out var property))
    {
      return property;
    }

    return null;
  }

  private static string? GetStringProperty(JsonElement? parameters, string propertyName)
  {
    var property = GetProperty(parameters, propertyName);
    return property is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
  }

  private static Task WriteAsync(HttpContext context, Response response)
  {
    return context.Response.WriteAsJsonAsync(response, SerializerOptions, cancellationToken: context.RequestAborted);
  }
}
