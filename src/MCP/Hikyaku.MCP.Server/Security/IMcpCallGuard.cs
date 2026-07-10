namespace Hikyaku.Kaido.MCP.Server.Security;

/// <summary>
/// Validates an incoming MCP call before it is dispatched. Register implementations in DI
/// (scoped) to add custom checks: token claims, per-tool authorization, rate limiting, etc.
/// All registered guards must allow the call; the first denial short-circuits with HTTP 401.
/// </summary>
public interface IMcpCallGuard
{
  Task<McpGuardResult> ValidateAsync(McpCallContext context, CancellationToken cancellationToken);
}

public class McpGuardResult
{
  private McpGuardResult(bool allowed, string? reason)
  {
    Allowed = allowed;
    Reason = reason;
  }

  public bool Allowed { get; }

  public string? Reason { get; }

  public static McpGuardResult Allow() => new(true, null);

  public static McpGuardResult Deny(string reason) => new(false, reason);
}
