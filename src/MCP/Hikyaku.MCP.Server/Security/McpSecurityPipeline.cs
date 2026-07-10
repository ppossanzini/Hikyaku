using System.Collections.Concurrent;
using System.Reflection;
using Hikyaku.MCP.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Hikyaku.MCP.Server.Security;

/// <summary>
/// Evaluates built-in checks (API key, authenticated user) and the custom <see cref="IMcpCallGuard"/> chain.
/// </summary>
internal static class McpSecurityPipeline
{
  internal static async Task<McpGuardResult> EvaluateAsync(McpCallContext context, McpSecurityOptions security, CancellationToken cancellationToken)
  {
    if (security.ApiKeyValidator is not null)
    {
      var apiKey = ExtractToken(context.HttpContext);
      if (string.IsNullOrEmpty(apiKey) || !await security.ApiKeyValidator(apiKey, context, cancellationToken))
      {
        return McpGuardResult.Deny("Missing or invalid API token.");
      }
    }

    if (security.AuthenticatedUserRequired && context.User.Identity?.IsAuthenticated != true)
    {
      return McpGuardResult.Deny("An authenticated user is required.");
    }

    foreach (var guard in context.HttpContext.RequestServices.GetServices<IMcpCallGuard>())
    {
      var result = await guard.ValidateAsync(context, cancellationToken);
      if (!result.Allowed)
      {
        return result;
      }
    }

    return McpGuardResult.Allow();
  }

  private static readonly ConcurrentDictionary<Type, (Type ServiceType, MethodInfo Enrich)> EnricherCache = new();

  internal static async Task EnrichAsync(object request, McpCallContext context, CancellationToken cancellationToken)
  {
    var (serviceType, enrich) = EnricherCache.GetOrAdd(request.GetType(), requestType =>
    {
      var service = typeof(IRequestEnrich<>).MakeGenericType(requestType);
      return (service, service.GetMethod(nameof(IRequestEnrich<object>.Enrich))!);
    });

    foreach (var enricher in context.HttpContext.RequestServices.GetServices(serviceType))
    {
      if (enricher is not null)
      {
        await (Task)enrich.Invoke(enricher, [request, cancellationToken])!;
      }
    }
  }

  private static string? ExtractToken(HttpContext context)
  {
    var authorization = context.Request.Headers.Authorization.FirstOrDefault();
    if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
      return authorization["Bearer ".Length..].Trim();
    }

    return context.Request.Headers["X-Api-Key"].FirstOrDefault();
  }
}
