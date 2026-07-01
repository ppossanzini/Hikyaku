using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hikyaku;
using Hikyaku.Kaido.MCP;
using Hikyaku.Kaido.MCP.Server.dto;
using Hikyaku.Kaido.MCP.Server.MCP;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace Hikyaku.Kaido.MCP.Server;

internal static class McpSupport
{
  internal sealed class DiscoveredComponent
  {
    public DiscoveredComponent(Type componentType, string name, string description, object inputSchema)
    {
      ComponentType = componentType;
      Name = name;
      Description = description;
      InputSchema = inputSchema;
    }

    [JsonIgnore]
    public Type ComponentType { get; }

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("description")]
    public string Description { get; }

    [JsonPropertyName("inputSchema")]
    public object InputSchema { get; }

    [JsonPropertyName("uri")]
    public string Uri => $"mcp://resource/{Name}";
  }

  internal static IReadOnlyList<object> DiscoverTools()
  {
    var discovered = DiscoverComponents<AgentToolAttribute>(attribute => new DiscoveredComponent(
      attribute.Type,
      GetComponentName(attribute.Type, attribute.ToolAttribute?.Title),
      attribute.ToolAttribute?.Description ?? string.Empty,
      BuildInputSchema(attribute.Type)));

    return discovered.Any() ? discovered.Cast<object>().ToArray() : [CreateWeatherTool()];
  }

  internal static IReadOnlyList<object> DiscoverResources()
  {
    return DiscoverComponents<AgentResourceAttribute>(attribute => new DiscoveredComponent(
      attribute.Type,
      GetComponentName(attribute.Type, attribute.ResourceAttribute?.Title),
      attribute.ResourceAttribute?.Description ?? string.Empty,
      BuildInputSchema(attribute.Type))).Cast<object>().ToArray();
  }

  internal static object BuildToolsListResult(IReadOnlyList<object> tools)
  {
    var toolsByName = tools.OfType<object>().ToDictionary(GetObjectName, tool => tool, StringComparer.OrdinalIgnoreCase);

    return new
    {
      Tools = toolsByName,
      tools
    };
  }

  internal static object BuildResourcesListResult(IReadOnlyList<object> resources)
  {
    var resourcesByName = resources.OfType<object>().ToDictionary(GetObjectName, resource => resource, StringComparer.OrdinalIgnoreCase);

    return new
    {
      Resources = resourcesByName,
      resources
    };
  }

  internal static async Task<object?> InvokeToolAsync(string name, object? parameters, IServiceProvider? serviceProvider, IHikyaku? hikyaku)
  {
    if (string.Equals(name, "get_meteo_citta", StringComparison.OrdinalIgnoreCase))
    {
      return CreateWeatherToolCallResult(parameters);
    }

    var tool = DiscoverComponents<AgentToolAttribute>(attribute => new DiscoveredComponent(
      attribute.Type,
      GetComponentName(attribute.Type, attribute.ToolAttribute?.Title),
      attribute.ToolAttribute?.Description ?? string.Empty,
      BuildInputSchema(attribute.Type))).FirstOrDefault(component => string.Equals(component.Name, name, StringComparison.OrdinalIgnoreCase));

    if (tool is null)
    {
      return null;
    }

    var componentInstance = DeserializeComponent(tool.ComponentType, GetArgumentsElement(parameters), serviceProvider);
    if (componentInstance is null)
    {
      return null;
    }

    if (hikyaku is not null && ImplementsRequestInterface(tool.ComponentType))
    {
      var invocationResult = await hikyaku.SendObject(componentInstance);
      return BuildInvocationResult(tool.Name, invocationResult);
    }

    return BuildInvocationResult(tool.Name, componentInstance);
  }

  internal static async Task<object?> InvokeResourceAsync(string name, object? parameters, IServiceProvider? serviceProvider, IHikyaku? hikyaku)
  {
    var resource = DiscoverComponents<AgentResourceAttribute>(attribute => new DiscoveredComponent(
      attribute.Type,
      GetComponentName(attribute.Type, attribute.ResourceAttribute?.Title),
      attribute.ResourceAttribute?.Description ?? string.Empty,
      BuildInputSchema(attribute.Type))).FirstOrDefault(component => string.Equals(component.Name, name, StringComparison.OrdinalIgnoreCase));

    if (resource is null)
    {
      return null;
    }

    var componentInstance = DeserializeComponent(resource.ComponentType, GetArgumentsElement(parameters), serviceProvider);
    if (componentInstance is null)
    {
      return null;
    }

    if (hikyaku is not null && ImplementsRequestInterface(resource.ComponentType))
    {
      var invocationResult = await hikyaku.SendObject(componentInstance);
      return BuildResourceReadResult(resource.Name, invocationResult);
    }

    return BuildResourceReadResult(resource.Name, componentInstance);
  }

  internal static object CreateWeatherToolCallResult(object? parameters)
  {
    var arguments = GetArgumentsElement(parameters);
    var city = GetString(arguments, "citta") ?? "Roma";
    var days = GetInt32(arguments, "giorni");
    var forecast = $"Previsioni meteo simulate per {city}{(days is null ? string.Empty : $" ({days} giorni)")}.";

    return new
    {
      Tools = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
      {
        ["get_meteo_citta"] = new
        {
          city,
          days,
          forecast
        }
      },
      content = new[]
      {
        new
        {
          type = "text",
          text = JsonSerializer.Serialize(new
          {
            city,
            days,
            forecast
          })
        }
      },
      isError = false,
      structuredContent = new
      {
        city,
        days,
        forecast
      }
    };
  }

  internal static object CreateWeatherTool()
  {
    return new McpTool
    {
      Name = "get_meteo_citta",
      Description = "Ottiene le informazioni meteo correnti per una specifica città italiana.",
      InputSchema = new
      {
        type = "object",
        properties = new
        {
          citta = new { type = "string", description = "Il nome della città (es. Roma, Milano)" },
          giorni = new { type = "integer", description = "Numero di giorni di previsione (opzionale)" }
        },
        required = new[] { "citta" }
      }
    };
  }

  private static IEnumerable<DiscoveredComponent> DiscoverComponents<TAttribute>(Func<ComponentCandidate, DiscoveredComponent> selector)
    where TAttribute : Attribute
  {
    foreach (var candidate in GetCandidateTypes())
    {
      var attribute = candidate.Type.GetCustomAttribute<TAttribute>(inherit: true);
      if (attribute is null)
      {
        continue;
      }

      yield return selector(candidate with { Attribute = attribute });
    }
  }

  private static IEnumerable<ComponentCandidate> GetCandidateTypes()
  {
    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
    {
      Type[] types;
      try
      {
        types = assembly.GetTypes();
      }
      catch (ReflectionTypeLoadException exception)
      {
        types = exception.Types.Where(type => type is not null).Cast<Type>().ToArray();
      }

      foreach (var type in types)
      {
        if (type is null || !type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
        {
          continue;
        }

        yield return new ComponentCandidate(type);
      }
    }
  }

  private static string GetObjectName(object value)
  {
    return value switch
    {
      DiscoveredComponent component => component.Name,
      McpTool tool => tool.Name,
      _ => value.GetType().GetProperty("Name")?.GetValue(value)?.ToString() ?? value.GetType().Name
    };
  }

  private static string GetComponentName(Type type, string? title)
  {
    return string.IsNullOrWhiteSpace(title) ? type.Name : title;
  }

  private static object BuildInputSchema(Type type)
  {
    var properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    var required = new List<string>();

    foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
      if (property.GetMethod is null || property.GetMethod.IsStatic)
      {
        continue;
      }

      var propertyName = GetJsonPropertyName(property);
      properties[propertyName] = BuildSchemaForType(property.PropertyType);

      if (IsRequiredProperty(property))
      {
        required.Add(propertyName);
      }
    }

    var schema = new Dictionary<string, object>
    {
      ["type"] = "object",
      ["properties"] = properties
    };

    if (required.Count > 0)
    {
      schema["required"] = required;
    }

    return schema;
  }

  private static object BuildSchemaForType(Type type)
  {
    var nullableType = Nullable.GetUnderlyingType(type);
    if (nullableType is not null)
    {
      type = nullableType;
    }

    if (type == typeof(string) || type == typeof(char))
    {
      return new Dictionary<string, object> { ["type"] = "string" };
    }

    if (type == typeof(bool))
    {
      return new Dictionary<string, object> { ["type"] = "boolean" };
    }

    if (type == typeof(decimal) || type == typeof(double) || type == typeof(float))
    {
      return new Dictionary<string, object> { ["type"] = "number" };
    }

    if (type == typeof(byte) || type == typeof(short) || type == typeof(int) || type == typeof(long) || type == typeof(sbyte) || type == typeof(ushort) || type == typeof(uint) || type == typeof(ulong))
    {
      return new Dictionary<string, object> { ["type"] = "integer" };
    }

    if (type.IsEnum)
    {
      return new Dictionary<string, object>
      {
        ["type"] = "string",
        ["enum"] = Enum.GetNames(type)
      };
    }

    if (type.IsArray)
    {
      return new Dictionary<string, object>
      {
        ["type"] = "array",
        ["items"] = BuildSchemaForType(type.GetElementType() ?? typeof(object))
      };
    }

    if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type.IsGenericType)
    {
      var elementType = type.GetGenericArguments().FirstOrDefault() ?? typeof(object);
      return new Dictionary<string, object>
      {
        ["type"] = "array",
        ["items"] = BuildSchemaForType(elementType)
      };
    }

    return new Dictionary<string, object> { ["type"] = "object" };
  }

  private static string GetJsonPropertyName(PropertyInfo property)
  {
    var jsonPropertyName = property.GetCustomAttribute<JsonPropertyNameAttribute>(inherit: true)?.Name;
    return string.IsNullOrWhiteSpace(jsonPropertyName) ? property.Name : jsonPropertyName;
  }

  private static bool IsRequiredProperty(PropertyInfo property)
  {
    if (property.PropertyType.IsValueType)
    {
      return Nullable.GetUnderlyingType(property.PropertyType) is null;
    }

    var nullableAttribute = property.CustomAttributes.FirstOrDefault(attribute => attribute.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");
    if (nullableAttribute is not null && nullableAttribute.ConstructorArguments.Count > 0)
    {
      var argument = nullableAttribute.ConstructorArguments[0];
      if (argument.ArgumentType == typeof(byte) && argument.Value is byte value)
      {
        return value != 2;
      }
    }

    return true;
  }

  private static object BuildInvocationResult(string name, object? invocationResult)
  {
    return new
    {
      Tools = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
      {
        [name] = invocationResult ?? new { }
      },
      content = new[]
      {
        new
        {
          type = "text",
          text = SerializeInvocationResult(invocationResult)
        }
      },
      isError = false,
      structuredContent = invocationResult
    };
  }

  private static object BuildResourceReadResult(string name, object? invocationResult)
  {
    return new
    {
      Resources = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
      {
        [name] = invocationResult ?? new { }
      },
      contents = new[]
      {
        new
        {
          uri = $"mcp://resource/{name}",
          mimeType = "application/json",
          text = SerializeInvocationResult(invocationResult)
        }
      }
    };
  }

  private static string SerializeInvocationResult(object? invocationResult)
  {
    if (invocationResult is null)
    {
      return string.Empty;
    }

    return invocationResult is string stringResult ? stringResult : JsonSerializer.Serialize(invocationResult);
  }

  private static bool ImplementsRequestInterface(Type componentType)
  {
    if (typeof(IRequest).IsAssignableFrom(componentType))
    {
      return true;
    }

    return componentType.GetInterfaces().Any(interfaceType => interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IRequest<>));
  }

  private static object? DeserializeComponent(Type componentType, JsonElement? arguments, IServiceProvider? serviceProvider)
  {
    if (arguments is not null && arguments.Value.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
    {
      try
      {
        var deserialized = JsonSerializer.Deserialize(arguments.Value.GetRawText(), componentType);
        if (deserialized is not null)
        {
          return deserialized;
        }
      }
      catch
      {
        // Fall back to construction below.
      }
    }

    try
    {
      if (serviceProvider is not null)
      {
        return ActivatorUtilities.CreateInstance(serviceProvider, componentType);
      }

      return Activator.CreateInstance(componentType);
    }
    catch
    {
      return null;
    }
  }

  private static JsonElement? GetArgumentsElement(object? parameters)
  {
    var requestElement = GetJsonElement(parameters);
    if (requestElement is null)
    {
      return null;
    }

    if (requestElement.Value.ValueKind == JsonValueKind.Object && requestElement.Value.TryGetProperty("arguments", out var arguments))
    {
      return arguments;
    }

    return requestElement;
  }

  private static JsonElement? GetJsonElement(object? value)
  {
    return value switch
    {
      null => null,
      JsonElement element => element,
      JsonDocument document => document.RootElement,
      _ => JsonSerializer.SerializeToElement(value)
    };
  }

  private static string? GetString(JsonElement? element, string propertyName)
  {
    if (element is null || element.Value.ValueKind != JsonValueKind.Object)
    {
      return null;
    }

    if (!element.Value.TryGetProperty(propertyName, out var propertyElement) || propertyElement.ValueKind != JsonValueKind.String)
    {
      return null;
    }

    return propertyElement.GetString();
  }

  private static int? GetInt32(JsonElement? element, string propertyName)
  {
    if (element is null || element.Value.ValueKind != JsonValueKind.Object)
    {
      return null;
    }

    if (!element.Value.TryGetProperty(propertyName, out var propertyElement))
    {
      return null;
    }

    if (propertyElement.ValueKind == JsonValueKind.Number && propertyElement.TryGetInt32(out var value))
    {
      return value;
    }

    return null;
  }

  private sealed record ComponentCandidate(Type Type)
  {
    public Attribute? Attribute { get; init; }

    public AgentToolAttribute? ToolAttribute => Attribute as AgentToolAttribute;

    public AgentResourceAttribute? ResourceAttribute => Attribute as AgentResourceAttribute;
  }
}
