using System.Text.Json;
using Hikyaku.MCP.Server;
using Xunit;

namespace Hikyaku.MCP.Server.Tests;

public class InputSchemaBuilderTests
{
  // ---- helpers -------------------------------------------------------------

  private static JsonElement BuildToolSchema(Type requestType)
  {
    var registry = new McpRegistry(new McpServerOptions().RegisterComponents(requestType));
    var tool = Assert.Single(registry.Tools);
    return JsonSerializer.SerializeToElement(tool.InputSchema);
  }

  private static JsonElement Prop(JsonElement schema, string name)
  {
    return schema.GetProperty("properties").GetProperty(name);
  }

  private static string TypeOf(JsonElement schema)
  {
    return schema.GetProperty("type").GetString()!;
  }

  private static string[] Required(JsonElement schema)
  {
    return schema.TryGetProperty("required", out var required)
      ? required.EnumerateArray().Select(e => e.GetString()!).ToArray()
      : Array.Empty<string>();
  }

  // ---- root ----------------------------------------------------------------

  [Fact]
  public void Root_IsObjectWithProperties()
  {
    var schema = BuildToolSchema(typeof(PrimitivesRequest));
    Assert.Equal("object", TypeOf(schema));
    Assert.True(schema.GetProperty("properties").EnumerateObject().Any());
  }

  // ---- primitives ----------------------------------------------------------

  [Fact]
  public void StringLike_Types_MapToString()
  {
    var schema = BuildToolSchema(typeof(PrimitivesRequest));
    Assert.Equal("string", TypeOf(Prop(schema, "text")));
    Assert.Equal("string", TypeOf(Prop(schema, "letter")));
    Assert.Equal("string", TypeOf(Prop(schema, "id")));
    Assert.Equal("string", TypeOf(Prop(schema, "createdAt")));
    Assert.Equal("string", TypeOf(Prop(schema, "updatedAt")));
    Assert.Equal("string", TypeOf(Prop(schema, "duration")));
    Assert.Equal("string", TypeOf(Prop(schema, "day")));
    Assert.Equal("string", TypeOf(Prop(schema, "clock")));
    Assert.Equal("string", TypeOf(Prop(schema, "homepage")));
  }

  [Fact]
  public void Numeric_Types_MapToNumberOrInteger()
  {
    var schema = BuildToolSchema(typeof(PrimitivesRequest));
    Assert.Equal("integer", TypeOf(Prop(schema, "number")));
    Assert.Equal("integer", TypeOf(Prop(schema, "bigNumber")));
    Assert.Equal("integer", TypeOf(Prop(schema, "smallNumber")));
    Assert.Equal("integer", TypeOf(Prop(schema, "tinyNumber")));
    Assert.Equal("number", TypeOf(Prop(schema, "amount")));
    Assert.Equal("number", TypeOf(Prop(schema, "ratio")));
    Assert.Equal("number", TypeOf(Prop(schema, "percent")));
  }

  [Fact]
  public void Boolean_MapsToBoolean()
  {
    var schema = BuildToolSchema(typeof(PrimitivesRequest));
    Assert.Equal("boolean", TypeOf(Prop(schema, "flag")));
  }

  [Fact]
  public void Enum_ExposesNamesAsStringValues()
  {
    var schema = BuildToolSchema(typeof(PrimitivesRequest));
    var enumSchema = Prop(schema, "priority");
    Assert.Equal("string", TypeOf(enumSchema));
    Assert.Equal(new[] { "Low", "Medium", "High" }, enumSchema.GetProperty("enum").EnumerateArray().Select(e => e.GetString()!).ToArray());
  }

  // ---- binary --------------------------------------------------------------

  [Theory]
  [InlineData("payload")]
  [InlineData("buffer")]
  [InlineData("readonlyBuffer")]
  public void BytePayloads_MapToBase64String(string propertyName)
  {
    var schema = BuildToolSchema(typeof(BinaryRequest));
    var property = Prop(schema, propertyName);
    Assert.Equal("string", TypeOf(property));
    Assert.Equal("byte", property.GetProperty("format").GetString());
  }

  // ---- dictionaries --------------------------------------------------------

  [Fact]
  public void GenericDictionary_MapsToObjectWithAdditionalProperties()
  {
    var schema = BuildToolSchema(typeof(DictionaryRequest));
    var map = Prop(schema, "map");
    Assert.Equal("object", TypeOf(map));
    Assert.Equal("integer", TypeOf(map.GetProperty("additionalProperties")));
  }

  [Fact]
  public void ReadOnlyDictionary_MapsToObjectWithAdditionalProperties()
  {
    var schema = BuildToolSchema(typeof(DictionaryRequest));
    var labels = Prop(schema, "labels");
    Assert.Equal("object", TypeOf(labels));
    Assert.Equal("string", TypeOf(labels.GetProperty("additionalProperties")));
  }

  [Fact]
  public void NonGenericDictionary_MapsToObjectWithObjectValues()
  {
    var schema = BuildToolSchema(typeof(DictionaryRequest));
    var bag = Prop(schema, "bag");
    Assert.Equal("object", TypeOf(bag));
    Assert.Equal("object", TypeOf(bag.GetProperty("additionalProperties")));
  }

  [Fact]
  public void DictionaryValueType_ExpandsRecursively()
  {
    var schema = BuildToolSchema(typeof(DictionaryRequest));
    var address = Prop(schema, "addressBook").GetProperty("additionalProperties");
    Assert.Equal("object", TypeOf(address));
    Assert.True(address.GetProperty("properties").TryGetProperty("street", out _));
    Assert.True(address.GetProperty("properties").TryGetProperty("zip_code", out _));
  }

  // ---- collections ---------------------------------------------------------

  [Fact]
  public void GenericCollections_MapToArrayWithItems()
  {
    var schema = BuildToolSchema(typeof(CollectionRequest));
    Assert.Equal("array", TypeOf(Prop(schema, "tags")));
    Assert.Equal("string", TypeOf(Prop(schema, "tags").GetProperty("items")));
    Assert.Equal("array", TypeOf(Prop(schema, "scores")));
    Assert.Equal("integer", TypeOf(Prop(schema, "scores").GetProperty("items")));
  }

  // ---- nullability / required ----------------------------------------------

  [Fact]
  public void Required_ComesFromNullability()
  {
    var schema = BuildToolSchema(typeof(PrimitivesRequest));
    var required = Required(schema);

    Assert.Contains("text", required);
    Assert.Contains("number", required);
    Assert.DoesNotContain("optionalText", required);
    Assert.DoesNotContain("optionalNumber", required);
  }

  [Fact]
  public void NullableNested_IsNotRequired_NonNullableCollection_IsRequired()
  {
    var schema = BuildToolSchema(typeof(ContactRequest));
    var required = Required(schema);
    Assert.DoesNotContain("address", required);
    Assert.Contains("addresses", required);
  }

  // ---- nesting / cycles ----------------------------------------------------

  [Fact]
  public void NestedComplexType_ExpandsRecursively()
  {
    var schema = BuildToolSchema(typeof(ContactRequest));
    var address = Prop(schema, "address");
    Assert.Equal("object", TypeOf(address));
    Assert.True(address.GetProperty("properties").TryGetProperty("street", out _));
    Assert.True(address.GetProperty("properties").TryGetProperty("zip_code", out _));
  }

  [Fact]
  public void SiblingProperties_OfSameType_AreBothExpanded()
  {
    var schema = BuildToolSchema(typeof(ContactRequest));

    var address = Prop(schema, "address");
    var addresses = Prop(schema, "addresses").GetProperty("items");

    Assert.Equal("object", TypeOf(address));
    Assert.Equal("object", TypeOf(addresses));
    Assert.True(address.GetProperty("properties").TryGetProperty("street", out _));
    Assert.True(addresses.GetProperty("properties").TryGetProperty("street", out _));
  }

  [Fact]
  public void CircularReference_BreaksWithOpaqueObject()
  {
    var schema = BuildToolSchema(typeof(CycleRequest));
    var root = Prop(schema, "root");
    Assert.Equal("object", TypeOf(root));
    Assert.True(root.GetProperty("properties").TryGetProperty("label", out _));

    // Node.Parent -> Node (currently expanding): opaque, no nested properties.
    var parent = root.GetProperty("properties").GetProperty("parent");
    Assert.Equal("object", TypeOf(parent));
    Assert.False(parent.TryGetProperty("properties", out _));

    // Node.Children -> List<Node>: items is Node again, so opaque too.
    var childrenItems = root.GetProperty("properties").GetProperty("children").GetProperty("items");
    Assert.Equal("object", TypeOf(childrenItems));
    Assert.False(childrenItems.TryGetProperty("properties", out _));
  }

  // ---- attributes ----------------------------------------------------------

  [Fact]
  public void Description_IsPropagatedToSchema()
  {
    var schema = BuildToolSchema(typeof(AttributeRequest));
    Assert.Equal("User display name.", Prop(schema, "name").GetProperty("description").GetString());
  }

  [Fact]
  public void JsonPropertyName_RenamesTheKey()
  {
    var schema = BuildToolSchema(typeof(AttributeRequest));
    Assert.True(Prop(schema, "the_id").TryGetProperty("type", out _));
    Assert.False(schema.GetProperty("properties").TryGetProperty("id", out _));
  }

  [Fact]
  public void JsonIgnoreAlways_ExcludesProperty()
  {
    var schema = BuildToolSchema(typeof(AttributeRequest));
    Assert.False(schema.GetProperty("properties").TryGetProperty("hidden", out _));
  }

  [Fact]
  public void ReadOnlyProperty_IsExcluded()
  {
    var schema = BuildToolSchema(typeof(AttributeRequest));
    Assert.False(schema.GetProperty("properties").TryGetProperty("readOnly", out _));
  }

  [Fact]
  public void InitOnlyProperty_IsIncluded()
  {
    var schema = BuildToolSchema(typeof(AttributeRequest));
    Assert.Equal("integer", TypeOf(Prop(schema, "initOnly")));
    Assert.Contains("initOnly", Required(schema));
  }

  // ---- opaque types --------------------------------------------------------

  [Theory]
  [InlineData("metadata")]  // object
  [InlineData("payload")]   // interface
  [InlineData("abstract")]  // abstract class
  public void ObjectInterfaceAbstract_AreOpaque(string propertyName)
  {
    var schema = BuildToolSchema(typeof(OpaqueRequest));
    var property = Prop(schema, propertyName);
    Assert.Equal("object", TypeOf(property));
    Assert.False(property.TryGetProperty("properties", out _));
  }
}
