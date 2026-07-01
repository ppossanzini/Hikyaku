using System.Text.Json;
using Hikyaku;
using Hikyaku.Kaido.MCP.Server.JsonRPC;
using Hikyaku.Kaido.MCP.Server.dto;
using Hikyaku.Kaido.MCP.Server.MCP;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

#nullable enable

namespace Hikyaku.Kaido.MCP.Server.Controllers;

[ApiController]
public class MCPController(IOptions<ServerInfo> serverInfo, IServiceProvider? serviceProvider = null, IHikyaku? hikyaku = null) : ControllerBase
{
  [Route("mcp")]
  public async Task<IActionResult> Handshake(Request request)
  {
    switch (request.Method)
    {
      case "initialize": return Initialize(request);
      case "tools/list": return ToolsList(request);
      case "tools/call": return await ToolsCall(request);
      case "resources/list": return ResourcesList(request);
      case "resources/read": return await ResourcesRead(request);
    }

    return BadRequest("Unsupported method");
  }


  public IActionResult Initialize(Request request)
  {
    return Ok(
      request.SuccessfulResponse(
        new MCP.InitializeResponse()
        {
          Capabilities = new Capabilities(),
          ServerInfo = serverInfo.Value
        }
      ));
  }


  public IActionResult ToolsList(Request request)
  {
    var tools = McpSupport.DiscoverTools();


    return Ok(new
    {
      jsonrpc = "2.0",
      id = request.Id,
      result = McpSupport.BuildToolsListResult(tools)
    });
  }

  public IActionResult ResourcesList(Request request)
  {
    var resources = McpSupport.DiscoverResources();

    return Ok(new
    {
      jsonrpc = "2.0",
      id = request.Id,
      result = McpSupport.BuildResourcesListResult(resources)
    });
  }

  public async Task<IActionResult> ToolsCall(Request request)
  {
    var name = GetRequestName(request.Params);
    if (string.IsNullOrWhiteSpace(name))
    {
      return Ok(request.ErrorResponse(ErrorCode.InvalidParams, "Missing tool name in tools/call payload."));
    }

    var result = await McpSupport.InvokeToolAsync(name, request.Params, serviceProvider, hikyaku);
    if (result is null)
    {
      return Ok(request.ErrorResponse(ErrorCode.MethodNotFound, $"Tool '{name}' was not found."));
    }

    return Ok(request.SuccessfulResponse(result));
  }

  public async Task<IActionResult> ResourcesRead(Request request)
  {
    var name = GetRequestName(request.Params);
    if (string.IsNullOrWhiteSpace(name))
    {
      return Ok(request.ErrorResponse(ErrorCode.InvalidParams, "Missing resource name in resources/read payload."));
    }

    var result = await McpSupport.InvokeResourceAsync(name, request.Params, serviceProvider, hikyaku);
    if (result is null)
    {
      return Ok(request.ErrorResponse(ErrorCode.MethodNotFound, $"Resource '{name}' was not found."));
    }

    return Ok(request.SuccessfulResponse(result));
  }

  private static string? GetRequestName(object? parameters)
  {
    return parameters switch
    {
      JsonElement element when element.ValueKind == JsonValueKind.Object && element.TryGetProperty("name", out var nameProperty) && nameProperty.ValueKind == JsonValueKind.String => nameProperty.GetString(),
      JsonElement element when element.ValueKind == JsonValueKind.Object && element.TryGetProperty("uri", out var uriProperty) && uriProperty.ValueKind == JsonValueKind.String => uriProperty.GetString()?.Split('/').LastOrDefault(),
      JsonElement element when element.ValueKind == JsonValueKind.Object && element.TryGetProperty("resource", out var resourceProperty) && resourceProperty.ValueKind == JsonValueKind.String => resourceProperty.GetString(),
      JsonDocument document => GetRequestName(document.RootElement),
      _ => null
    };
  }
}