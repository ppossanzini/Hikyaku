using Hikyaku;

namespace McpExample;

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

public class SumNumbersToolHandler : IRequestHandler<SumNumbersTool, SumNumbersResult>
{
  public Task<SumNumbersResult> Handle(SumNumbersTool request, CancellationToken cancellationToken)
  {
    return Task.FromResult(new SumNumbersResult { Sum = request.A + request.B });
  }
}

public class ServerMetadataResourceHandler : IRequestHandler<ServerMetadataResource, ServerMetadataResult>
{
  public Task<ServerMetadataResult> Handle(ServerMetadataResource request, CancellationToken cancellationToken)
  {
    var result = new ServerMetadataResult
    {
      Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
      Machine = Environment.MachineName,
      ClientName = request.ClientName
    };

    return Task.FromResult(result);
  }
}
