using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Hikyaku.Kaido.MCP.Server;

/// <summary>
/// Builds a JSON Schema describing the public settable properties of a request type,
/// used as the MCP tool input schema.
/// </summary>
internal static class InputSchemaBuilder
{
  internal static object Build(Type type)
  {
    var properties = new Dictionary<string, object>();
    var required = new List<string>();

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
      var propertySchema = BuildSchemaForType(property.PropertyType);

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

  private static Dictionary<string, object> BuildSchemaForType(Type type)
  {
    type = Nullable.GetUnderlyingType(type) ?? type;

    if (type == typeof(string) || type == typeof(char) || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan))
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

    if (type != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type.IsGenericType)
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
