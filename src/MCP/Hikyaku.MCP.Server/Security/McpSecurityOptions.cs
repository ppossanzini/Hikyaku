namespace Hikyaku.Kaido.MCP.Server.Security;

/// <summary>
/// Validates the API key presented by the caller ("Authorization: Bearer &lt;key&gt;" or "X-Api-Key" header).
/// Implement the check however fits your setup: constant-time comparison against a configured secret,
/// database lookup, token introspection endpoint, etc.
/// </summary>
public delegate Task<bool> McpApiKeyValidator(string apiKey, McpCallContext context, CancellationToken cancellationToken);

/// <summary>
/// Built-in security checks applied to every MCP call before custom guards run.
/// When nothing is configured the endpoint accepts anonymous calls.
/// </summary>
public class McpSecurityOptions
{
  internal McpApiKeyValidator ApiKeyValidator { get; private set; }

  internal bool AuthenticatedUserRequired { get; private set; }

  /// <summary>
  /// Requires callers to present an API key, either as "Authorization: Bearer &lt;key&gt;" or in the
  /// "X-Api-Key" header. The key is passed to <paramref name="validator"/>, which decides whether the
  /// call is allowed. Calls without a key are rejected without invoking the validator.
  /// Calling this method again replaces the previous validator.
  /// </summary>
  public McpSecurityOptions WithApiKey(McpApiKeyValidator validator)
  {
    ApiKeyValidator = validator ?? throw new ArgumentNullException(nameof(validator));
    return this;
  }

  /// <summary>
  /// Same as <see cref="WithApiKey(McpApiKeyValidator)"/> for validators that do not need the call context.
  /// </summary>
  public McpSecurityOptions WithApiKey(Func<string, Task<bool>> validator)
  {
    ArgumentNullException.ThrowIfNull(validator);
    return WithApiKey((apiKey, _, _) => validator(apiKey));
  }

  /// <summary>
  /// Same as <see cref="WithApiKey(McpApiKeyValidator)"/> for synchronous validators.
  /// </summary>
  public McpSecurityOptions WithApiKey(Func<string, bool> validator)
  {
    ArgumentNullException.ThrowIfNull(validator);
    return WithApiKey((apiKey, _, _) => Task.FromResult(validator(apiKey)));
  }

  /// <summary>
  /// Requires an authenticated <c>HttpContext.User</c>. Pair this with the ASP.NET Core
  /// authentication middleware (e.g. JWT bearer) so claims are available to guards and enrichers.
  /// </summary>
  public McpSecurityOptions RequireAuthenticatedUser()
  {
    AuthenticatedUserRequired = true;
    return this;
  }
}
