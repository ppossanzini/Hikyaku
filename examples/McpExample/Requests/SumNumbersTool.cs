using System.ComponentModel;
using Hikyaku;
using Hikyaku.Kaido.MCP;
using McpExample.DTO;

namespace McpExample.Requests;

[AgentTool(Name = "sum_numbers",
  Title = "Sum two numbers",
  Description = "Adds two integers and returns the sum. Useful to verify tool invocation end to end.")]
public class SumNumbersTool : IRequest<SumNumbersResult>
{
  [Description("First addend.")]
  public int A { get; set; }

  [Description("Second addend.")]
  public int B { get; set; }
}
