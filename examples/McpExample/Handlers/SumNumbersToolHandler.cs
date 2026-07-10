using Hikyaku;
using McpExample.DTO;
using McpExample.Requests;

namespace McpExample.Handlers;

public class SumNumbersToolHandler : IRequestHandler<SumNumbersTool, SumNumbersResult>
{
  public Task<SumNumbersResult> Handle(SumNumbersTool request, CancellationToken cancellationToken)
  {
    return Task.FromResult(new SumNumbersResult { Sum = request.A + request.B });
  }
}
