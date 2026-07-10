using System.Reflection;
using Hikyaku.MCP.Server.Security;
using Hikyaku.MCP;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Hikyaku.MCP.Server.Extensions;

public static class Extensions
{
  /// <summary>
  /// Registers the Hikyaku MCP server services. Tools and resources are discovered from the
  /// assemblies registered in the options (or from all loaded assemblies when none is specified).
  /// </summary>
  public static IServiceCollection AddHikyakuMcpServer(this IServiceCollection services, Action<McpServerOptions>? configure = null)
  {
    services.AddSingleton(_ =>
    {
      var options = new McpServerOptions();
      var entryAssembly = Assembly.GetEntryAssembly();
      options.ServerInfo.Name = entryAssembly?.GetName().Name ?? "Hikyaku MCP Server";
      options.ServerInfo.Version = entryAssembly?.GetName().Version?.ToString() ?? "1.0.0";
      configure?.Invoke(options);
      return options;
    });

    services.AddSingleton<McpRegistry>();
    return services;
  }

  /// <summary>
  /// Registers a guard that validates every MCP call before it is dispatched (e.g. token claims checks).
  /// </summary>
  public static IServiceCollection AddMcpCallGuard<TGuard>(this IServiceCollection services) where TGuard : class, IMcpCallGuard
  {
    services.AddScoped<IMcpCallGuard, TGuard>();
    return services;
  }

  /// <summary>
  /// Registers an <see cref="IRequestEnrich{TRequest}"/> implementation, invoked on the materialized
  /// request before it is dispatched through Hikyaku (e.g. to copy token claims into the request).
  /// </summary>
  public static IServiceCollection AddMcpRequestEnricher<TEnricher>(this IServiceCollection services) where TEnricher : class
  {
    return services.AddMcpRequestEnricher(typeof(TEnricher));
  }

  /// <summary>
  /// Registers an <see cref="IRequestEnrich{TRequest}"/> implementation. Accepts:
  /// <list type="bullet">
  ///   <item>A closed concrete type (e.g. <c>typeof(ClientNameEnricher)</c>) — registered for the specific request type it targets.</item>
  ///   <item>An open generic type (e.g. <c>typeof(AuditEnricher&lt;&gt;)</c>) — applied to every request type.</item>
  ///   <item>The open generic interface itself (<c>typeof(IRequestEnrich&lt;&gt;)</c>) — scans <paramref name="assemblies"/> (or all loaded assemblies when none are specified) and registers every concrete implementation found.</item>
  /// </list>
  /// </summary>
  public static IServiceCollection AddMcpRequestEnricher(this IServiceCollection services, Type enricherType, params Assembly[] assemblies)
  {
    if (enricherType.IsGenericTypeDefinition && enricherType == typeof(IRequestEnrich<>))
    {
      var searchAssemblies = assemblies.Length > 0
        ? assemblies
        : AppDomain.CurrentDomain.GetAssemblies();

      var implementations = searchAssemblies
        .SelectMany(a => { try { return a.GetTypes(); } catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null).Cast<Type>(); } })
        .Where(t => t is { IsAbstract: false, IsInterface: false })
        .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestEnrich<>)));

      foreach (var impl in implementations)
        services.AddMcpRequestEnricher(impl);

      return services;
    }

    var implementedEnrichInterfaces = enricherType.GetInterfaces()
      .Where(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IRequestEnrich<>))
      .ToArray();

    if (implementedEnrichInterfaces.Length == 0)
    {
      throw new ArgumentException($"Type '{enricherType.FullName}' does not implement IRequestEnrich<TRequest>.", nameof(enricherType));
    }

    if (enricherType.IsGenericTypeDefinition)
    {
      services.AddScoped(typeof(IRequestEnrich<>), enricherType);
      return services;
    }

    foreach (var enrichInterface in implementedEnrichInterfaces)
    {
      services.AddScoped(enrichInterface, enricherType);
    }

    return services;
  }

  /// <summary>
  /// Maps the MCP JSON-RPC endpoint (streamable HTTP transport) on the given pattern.
  /// Chain ASP.NET Core policies (e.g. .RequireAuthorization()) on the returned builder if needed.
  /// </summary>
  public static IEndpointConventionBuilder MapHikyakuMcpServer(this IEndpointRouteBuilder endpoints, string pattern = "/mcp")
  {
    return endpoints.MapPost(pattern, McpEndpoint.HandleAsync);
  }
}
