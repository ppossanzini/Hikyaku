using System.Reflection;
using Hikyaku.Kaido.MCP.Server.Security;

namespace Hikyaku.Kaido.MCP.Server;

public class McpServerOptions
{
  /// <summary>
  /// Server identity returned by the MCP "initialize" handshake.
  /// </summary>
  public ServerInfo ServerInfo { get; } = new();

  /// <summary>
  /// Built-in security checks (API key, authenticated user) applied to every MCP call.
  /// For custom logic register <see cref="IMcpCallGuard"/> / <see cref="IMcpRequestEnricher"/> services in DI.
  /// </summary>
  public McpSecurityOptions Security { get; } = new();

  internal bool ExposeAllLoadedComponents { get; private set; }

  internal HashSet<Assembly> Assemblies { get; } = new();

  internal HashSet<Type> Types { get; } = new();

  /// <summary>
  /// Exposes every [AgentTool] / [AgentResource] request type found in the assemblies
  /// loaded in the current AppDomain. This is also the default when nothing is registered.
  /// </summary>
  public McpServerOptions RegisterAllComponents()
  {
    ExposeAllLoadedComponents = true;
    return this;
  }

  /// <summary>
  /// Exposes every [AgentTool] / [AgentResource] request type found in the given assemblies.
  /// </summary>
  public McpServerOptions RegisterComponentsFromAssemblies(params Assembly[] assemblies)
  {
    foreach (var assembly in assemblies)
    {
      Assemblies.Add(assembly);
    }

    return this;
  }

  /// <summary>
  /// Exposes every [AgentTool] / [AgentResource] request type found in the given assembly.
  /// </summary>
  public McpServerOptions RegisterComponentsFromAssembly(Assembly assembly)
  {
    return RegisterComponentsFromAssemblies(assembly);
  }

  /// <summary>
  /// Exposes every [AgentTool] / [AgentResource] request type found in the assembly containing <typeparamref name="T"/>.
  /// </summary>
  public McpServerOptions RegisterComponentsFromAssemblyContaining<T>()
  {
    return RegisterComponentsFromAssemblies(typeof(T).Assembly);
  }

  /// <summary>
  /// Exposes exactly the given request types. Each type must be decorated with
  /// [AgentTool] or [AgentResource]; anything else fails at startup.
  /// </summary>
  public McpServerOptions RegisterComponents(params Type[] types)
  {
    foreach (var type in types)
    {
      Types.Add(type);
    }

    return this;
  }

  /// <summary>
  /// Exposes the request type <typeparamref name="T"/>, which must be decorated with [AgentTool] or [AgentResource].
  /// </summary>
  public McpServerOptions RegisterComponent<T>() where T : IBaseRequest
  {
    return RegisterComponents(typeof(T));
  }
}
