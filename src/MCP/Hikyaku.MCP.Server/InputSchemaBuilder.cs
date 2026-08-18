using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Hikyaku.MCP.Server;

/// <summary>
/// Builds a JSON Schema describing the public settable properties of a request type,
/// used as the MCP tool input schema. Nested complex types are expanded recursively;
/// circular references are broken with an opaque object schema.
/// </summary>
internal static class InputSchemaBuilder
{
  internal static object Build(Type type)
  {
    return Build(type, new HashSet<Type>());
  }

  private static Dictionary<string, object> Build(Type type, HashSet<Type> expanding)
  {
    var properties = new Dictionary<string, object>();
    var required = new List<string>();

    if (!expanding.Add(type))
    {
      // Circular reference (direct or indirect): emit an opaque object to break the cycle.
      return new Dictionary<string, object> { ["type"] = "object" };
    }

    try
    {
      foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
      {
        if (property.SetMethod is null || property.SetMethod.IsStatic)
        {
          continue;
        }

        if (property.GetCustomAttribute<JsonIgnoreAttribute>(inherit: true) is { Condition: JsonIgnoreCondition.Always })
        {
          continue;
        }

        var propertyName = GetJsonPropertyName(property);
        var propertySchema = BuildSchemaForType(property.PropertyType, expanding);

        var description = property.GetCustomAttribute<DescriptionAttribute>(inherit: true)?.Description;
        if (!string.IsNullOrWhiteSpace(description))
        {
          propertySchema["description"] = description;
        }

        properties[propertyName] = propertySchema;

        if (IsRequiredProperty(property))
        {
          required.Add(propertyName);
        }
      }
    }
    finally
    {
      expanding.Remove(type);
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

  private static Dictionary<string, object> BuildSchemaForType(Type type, HashSet<Type> expanding)
  {
    type = Nullable.GetUnderlyingType(type) ?? type;

    if (type == typeof(string) || type == typeof(char) || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(Uri))
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

    // byte[] (and byte[]-like) is serialized by System.Text.Json as a base64 string, not an array.
    if (type == typeof(byte[]) || type == typeof(Memory<byte>) || type == typeof(ReadOnlyMemory<byte>))
    {
      return new Dictionary<string, object>
      {
        ["type"] = "string",
        ["format"] = "byte"
      };
    }

    if (type.IsArray)
    {
      return new Dictionary<string, object>
      {
        ["type"] = "array",
        ["items"] = BuildSchemaForType(type.GetElementType() ?? typeof(object), expanding)
      };
    }

    var dictionaryInfo = GetDictionaryInfo(type);
    if (dictionaryInfo.IsDictionary)
    {
      // JSON object keys are always strings, so only the value type matters for additionalProperties.
      return new Dictionary<string, object>
      {
        ["type"] = "object",
        ["additionalProperties"] = BuildSchemaForType(dictionaryInfo.ValueType, expanding)
      };
    }

    if (type != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type.IsGenericType)
    {
      var elementType = type.GetGenericArguments().FirstOrDefault() ?? typeof(object);
      return new Dictionary<string, object>
      {
        ["type"] = "array",
        ["items"] = BuildSchemaForType(elementType, expanding)
      };
    }

    if (type == typeof(object) || type.IsAbstract || type.IsInterface)
    {
      return new Dictionary<string, object> { ["type"] = "object" };
    }

    // Complex type: expand its public settable properties recursively.
    return Build(type, expanding);
  }

  private static (bool IsDictionary, Type ValueType) GetDictionaryInfo(Type type)
  {
    if (type == typeof(System.Collections.IDictionary))
    {
      return (true, typeof(object));
    }

    foreach (var candidate in type.GetInterfaces().Append(type))
    {
      if (!candidate.IsGenericType)
      {
        continue;
      }

      var definition = candidate.GetGenericTypeDefinition();
      if (definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>))
      {
        return (true, candidate.GetGenericArguments()[1]);
      }
    }

    return (false, typeof(object));
  }

  private static string GetJsonPropertyName(PropertyInfo property)
  {
    var jsonPropertyName = property.GetCustomAttribute<JsonPropertyNameAttribute>(inherit: true)?.Name;
    return string.IsNullOrWhiteSpace(jsonPropertyName) ? System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(property.Name) : jsonPropertyName;
  }

  private static bool IsRequiredProperty(PropertyInfo property)
  {
    if (property.PropertyType.IsValueType)
    {
      return Nullable.GetUnderlyingType(property.PropertyType) is null;
    }

    var nullabilityInfo = new NullabilityInfoContext().Create(property);
    return nullabilityInfo.WriteState == NullabilityState.NotNull;
  }
}
