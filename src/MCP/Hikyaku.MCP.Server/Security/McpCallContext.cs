using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Hikyaku.Kaido.MCP.Server.Security;

/// <summary>
/// Context of an incoming MCP call, made available to guards and enrichers.
/// </summary>
public class McpCallContext
{
  /// <summary>
  /// The underlying HTTP context (headers, token, connection info).
  /// </summary>
  public required HttpContext HttpContext { get; init; }

  /// <summary>
  /// The JSON-RPC method being invoked (e.g. "tools/call", "resources/read").
  /// </summary>
  public required string Method { get; init; }

  /// <summary>
  /// The tool name or resource uri targeted by the call, when present in the params.
  /// </summary>
  public string ComponentName { get; init; }

  /// <summary>
  /// The authenticated principal, populated by the ASP.NET Core authentication middleware when configured.
  /// </summary>
  public ClaimsPrincipal User => HttpContext.User;
}
