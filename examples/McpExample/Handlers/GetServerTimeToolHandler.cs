using Hikyaku;
using McpExample.DTO;
using McpExample.Requests;

namespace McpExample.Handlers;

public class GetServerTimeToolHandler : IRequestHandler<GetServerTimeTool, GetServerTimeResult>
{
  public Task<GetServerTimeResult> Handle(GetServerTimeTool request, CancellationToken cancellationToken)
  {
    var result = new GetServerTimeResult
    {
      UtcNow = DateTime.UtcNow,
      TimeZoneHint = request.TimeZoneHint
    };

    return Task.FromResult(result);
  }
}
