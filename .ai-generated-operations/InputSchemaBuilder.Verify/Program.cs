using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hikyaku;
using Hikyaku.MCP.Contracts;
using Hikyaku.MCP.Server;

namespace InputSchemaBuilder.Verify;

public enum Priority { Low, Medium, High }

public class Address
{
  [Description("Street name.")]
  public string Street { get; set; } = string.Empty;

  [JsonPropertyName("zip_code")]
  public string ZipCode { get; set; } = string.Empty;

  public string? OptionalNote { get; set; }
}

public class Contact
{
  public string Name { get; set; } = string.Empty;
  public Address? Address { get; set; }
  public List<Address> Addresses { get; set; } = new();
  public Dictionary<string, int> Scores { get; set; } = new();
  public Priority Priority { get; set; }
  public byte[] Payload { get; set; } = Array.Empty<byte>();
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }
  public Guid Id { get; set; }
  public int? Age { get; set; }
  public bool IsActive { get; set; }
  public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
  public object? Metadata { get; set; }
  public Node? Next { get; set; }
  public Contact? Self { get; set; }
  public int ReadOnlyComputed => 42;
  public string Field { get; set; } = string.Empty;
}

public class Node
{
  public string Label { get; set; } = string.Empty;
  public Node? Parent { get; set; }
  public List<Node> Children { get; set; } = new();
}

public interface IPayload { }

public class AbstractPayload : IPayload
{
  public string TypeName { get; set; } = string.Empty;
}

public class VerifyContactResult
{
  public bool Ok { get; set; }
}

[AgentTool(Name = "verify_contact", Title = "Verify contact", Description = "Verification harness.")]
public class VerifyContactTool : IRequest<VerifyContactResult>
{
  public Contact Contact { get; set; } = new();

  [JsonIgnore]
  public string Hidden { get; set; } = string.Empty;

  public IPayload? Payload { get; set; }

  public AbstractPayload? Abstract { get; set; }

  public Uri? Homepage { get; set; }
}

public static class Program
{
  public static void Main()
  {
    var options = new McpServerOptions().RegisterComponents(typeof(VerifyContactTool));
    var registry = new McpRegistry(options);

    foreach (var tool in registry.Tools)
    {
      Console.WriteLine($"=== {tool.Name} ===");
      Console.WriteLine(JsonSerializer.Serialize(tool.InputSchema, new JsonSerializerOptions { WriteIndented = true }));
    }

    Console.WriteLine();
    Console.WriteLine("=== Round-trip with server-like serializer options ===");
    var serverOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
      DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
      Converters = { new JsonStringEnumConverter() }
    };
    var json = """{"contact":{"priority":"High","scores":{"math":5},"payload":"AQID"}}""";
    var deserialized = JsonSerializer.Deserialize<VerifyContactTool>(json, serverOptions);
    Console.WriteLine($"priority={deserialized!.Contact.Priority} scores[math]={deserialized.Contact.Scores["math"]} payload={Convert.ToBase64String(deserialized.Contact.Payload)}");
    Console.WriteLine($"reserialized: {JsonSerializer.Serialize(deserialized, serverOptions)}");
  }
}
