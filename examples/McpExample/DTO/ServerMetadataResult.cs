namespace McpExample.DTO;

public class ServerMetadataResult
{
  public string Environment { get; set; } = string.Empty;
  public string Machine { get; set; } = string.Empty;
  public string? ClientName { get; set; }
}
