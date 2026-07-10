using System.Reflection;
using System.Text;
using Hikyaku.MCP.Server.Descriptors;
using Hikyaku.MCP;

namespace Hikyaku.MCP.Server;

/// <summary>
/// Discovers [AgentTool] / [AgentResource] request types once at startup and indexes them for MCP dispatching.
/// </summary>
public class McpRegistry
{
  private readonly Dictionary<string, ToolDescriptor> _toolsByName;
  private readonly Dictionary<string, ResourceDescriptor> _resourcesByUri;

  public McpRegistry(McpServerOptions options)
  {
    _toolsByName = new Dictionary<string, ToolDescriptor>(StringComparer.OrdinalIgnoreCase);
    _resourcesByUri = new Dictionary<string, ResourceDescriptor>(StringComparer.OrdinalIgnoreCase);

    foreach (var type in GetCandidateTypes(options))
    {
      var toolAttribute = type.GetCustomAttribute<AgentToolAttribute>(inherit: false);
      if (toolAttribute is not null)
      {
        EnsureIsRequest(type, nameof(AgentToolAttribute));
        var name = toolAttribute.Name ?? ToSnakeCase(type.Name);
        if (!_toolsByName.TryAdd(name, new ToolDescriptor
            {
              Name = name,
              Title = toolAttribute.Title,
              Description = toolAttribute.Description,
              RequestType = type,
              InputSchema = InputSchemaBuilder.Build(type)
            }))
        {
          throw new InvalidOperationException($"Duplicate MCP tool name '{name}' declared by '{type.FullName}' and '{_toolsByName[name].RequestType.FullName}'.");
        }
      }

      var resourceAttribute = type.GetCustomAttribute<AgentResourceAttribute>(inherit: false);
      if (resourceAttribute is not null)
      {
        EnsureIsRequest(type, nameof(AgentResourceAttribute));
        var name = resourceAttribute.Name ?? ToSnakeCase(type.Name);
        var uri = resourceAttribute.Uri ?? $"hikyaku://resources/{name}";
        if (!_resourcesByUri.TryAdd(uri, new ResourceDescriptor
            {
              Name = name,
              Title = resourceAttribute.Title,
              Description = resourceAttribute.Description,
              Uri = uri,
              MimeType = resourceAttribute.MimeType,
              RequestType = type
            }))
        {
          throw new InvalidOperationException($"Duplicate MCP resource uri '{uri}' declared by '{type.FullName}' and '{_resourcesByUri[uri].RequestType.FullName}'.");
        }
      }
    }
  }

  public IReadOnlyCollection<ToolDescriptor> Tools => _toolsByName.Values;

  public IReadOnlyCollection<ResourceDescriptor> Resources => _resourcesByUri.Values;

  public bool TryGetTool(string name, out ToolDescriptor tool)
  {
    return _toolsByName.TryGetValue(name, out tool!);
  }

  public bool TryGetResource(string uri, out ResourceDescriptor resource)
  {
    return _resourcesByUri.TryGetValue(uri, out resource!);
  }

  private static IEnumerable<Type> GetCandidateTypes(McpServerOptions options)
  {
    var seen = new HashSet<Type>();

    // Mode 3: explicitly registered types. They must carry one of the agent attributes.
    foreach (var type in options.Types)
    {
      if (type.GetCustomAttribute<AgentToolAttribute>(inherit: false) is null &&
          type.GetCustomAttribute<AgentResourceAttribute>(inherit: false) is null)
      {
        throw new InvalidOperationException($"Type '{type.FullName}' was registered as an MCP component but is not decorated with [AgentTool] or [AgentResource].");
      }

      if (seen.Add(type))
      {
        yield return type;
      }
    }

    // Mode 2: scan the registered assemblies. Mode 1 (explicit or by default when
    // nothing is registered): scan every assembly loaded in the AppDomain.
    var scanEverything = options.ExposeAllLoadedComponents || (options.Assemblies.Count == 0 && options.Types.Count == 0);
    var assemblies = scanEverything
      ? AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic)
      : options.Assemblies;

    foreach (var assembly in assemblies)
    {
      Type?[] types;
      try
      {
        types = assembly.GetTypes();
      }
      catch (ReflectionTypeLoadException exception)
      {
        types = exception.Types;
      }

      foreach (var type in types)
      {
        if (type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } && seen.Add(type))
        {
          yield return type;
        }
      }
    }
  }

  private static void EnsureIsRequest(Type type, string attributeName)
  {
    if (!typeof(IBaseRequest).IsAssignableFrom(type))
    {
      throw new InvalidOperationException($"Type '{type.FullName}' is decorated with [{attributeName}] but does not implement IRequest or IRequest<TResponse>.");
    }
  }

  internal static string ToSnakeCase(string value)
  {
    var builder = new StringBuilder(value.Length + 8);
    for (var i = 0; i < value.Length; i++)
    {
      var character = value[i];
      if (char.IsUpper(character))
      {
        if (i > 0 && (!char.IsUpper(value[i - 1]) || (i + 1 < value.Length && char.IsLower(value[i + 1]))))
        {
          builder.Append('_');
        }

        builder.Append(char.ToLowerInvariant(character));
      }
      else
      {
        builder.Append(character);
      }
    }

    return builder.ToString();
  }
}
