using System.ComponentModel;
using System.Text.Json.Serialization;
using Hikyaku;
using Hikyaku.MCP.Contracts;

namespace Hikyaku.MCP.Server.Tests;

public enum Priority { Low, Medium, High }

public class Address
{
  [Description("Street name.")]
  public string Street { get; set; } = string.Empty;

  [JsonPropertyName("zip_code")]
  public string ZipCode { get; set; } = string.Empty;
}

public class Node
{
  public string Label { get; set; } = string.Empty;

  public Node? Parent { get; set; }

  public List<Node> Children { get; set; } = new();
}

public interface IPayload { }

public abstract class AbstractPayload
{
  public string TypeName { get; set; } = string.Empty;
}

/// <summary>Covers scalar primitives, enums, dates, Uri and nullability.</summary>
[AgentTool(Name = "test_primitives")]
public class PrimitivesRequest : IRequest
{
  public string Text { get; set; } = string.Empty;
  public string? OptionalText { get; set; }
  public int Number { get; set; }
  public int? OptionalNumber { get; set; }
  public long BigNumber { get; set; }
  public short SmallNumber { get; set; }
  public byte TinyNumber { get; set; }
  public bool Flag { get; set; }
  public decimal Amount { get; set; }
  public double Ratio { get; set; }
  public float Percent { get; set; }
  public Guid Id { get; set; }
  public char Letter { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTimeOffset UpdatedAt { get; set; }
  public TimeSpan Duration { get; set; }
  public DateOnly Day { get; set; }
  public TimeOnly Clock { get; set; }
  public Uri? Homepage { get; set; }
  public Priority Priority { get; set; }
}

/// <summary>Covers byte payloads, serialized by System.Text.Json as base64 strings.</summary>
[AgentTool(Name = "test_binary")]
public class BinaryRequest : IRequest
{
  public byte[] Payload { get; set; } = Array.Empty<byte>();
  public Memory<byte> Buffer { get; set; }
  public ReadOnlyMemory<byte> ReadonlyBuffer { get; set; }
}

/// <summary>Covers generic, read-only and non-generic dictionaries.</summary>
[AgentTool(Name = "test_dictionaries")]
public class DictionaryRequest : IRequest
{
  public Dictionary<string, int> Map { get; set; } = new();
  public IReadOnlyDictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();
  public System.Collections.IDictionary? Bag { get; set; }
  public Dictionary<string, Address> AddressBook { get; set; } = new();
}

/// <summary>Covers single-dimensional generic collections.</summary>
[AgentTool(Name = "test_collections")]
public class CollectionRequest : IRequest
{
  public List<string> Tags { get; set; } = new();
  public IReadOnlyList<int> Scores { get; set; } = Array.Empty<int>();
  public List<Address> Addresses { get; set; } = new();
}

/// <summary>Covers nested expansion and sibling types of the same class.</summary>
[AgentTool(Name = "test_contact")]
public class ContactRequest : IRequest
{
  public Address? Address { get; set; }
  public List<Address> Addresses { get; set; } = new();
}

/// <summary>Covers direct and indirect circular references.</summary>
[AgentTool(Name = "test_cycle")]
public class CycleRequest : IRequest
{
  public Node? Root { get; set; }
}

/// <summary>Covers description, JSON name mapping, ignore and setter policies.</summary>
[AgentTool(Name = "test_attributes")]
public class AttributeRequest : IRequest
{
  [Description("User display name.")]
  public string Name { get; set; } = string.Empty;

  [JsonPropertyName("the_id")]
  public int Id { get; set; }

  [JsonIgnore]
  public string Hidden { get; set; } = string.Empty;

  public string ReadOnly { get; } = string.Empty;

  public int InitOnly { get; init; }
}

/// <summary>Covers object, interface and abstract properties.</summary>
[AgentTool(Name = "test_opaque")]
public class OpaqueRequest : IRequest
{
  public object? Metadata { get; set; }
  public IPayload? Payload { get; set; }
  public AbstractPayload? Abstract { get; set; }
}
