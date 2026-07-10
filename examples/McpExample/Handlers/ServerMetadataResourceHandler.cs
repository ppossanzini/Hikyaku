using Hikyaku;
using McpExample.DTO;
using McpExample.Requests;

namespace McpExample.Handlers;

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
