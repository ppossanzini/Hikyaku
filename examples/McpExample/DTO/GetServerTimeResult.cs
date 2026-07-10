namespace McpExample.DTO;

public class GetServerTimeResult
{
  public DateTime UtcNow { get; set; }
  public string? TimeZoneHint { get; set; }
}
