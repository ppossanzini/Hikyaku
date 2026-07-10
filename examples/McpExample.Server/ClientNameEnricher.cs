using Hikyaku.Kaido.MCP;
using McpExample;
using McpExample.Requests;

namespace McpExample.Server;

/// <summary>
/// Example enricher: copies the caller identity (authenticated user name or X-Client-Name header)
/// into requests that carry a ClientName, so handlers never touch the HTTP layer,
/// and data passed to the handler can be enriched with information from the HTTP request (claims, headers, etc.) without coupling the handler to ASP.NET Core.
/// Being a scoped DI service, it injects IHttpContextAccessor to reach the current call.
/// </summary>
public class ClientNameEnricher(IHttpContextAccessor httpContextAccessor) : 
  IRequestEnrich<ServerMetadataResource>
{
  public Task Enrich(ServerMetadataResource request, CancellationToken cancellationToken)
  {
    var httpContext = httpContextAccessor.HttpContext;
    request.ClientName = httpContext?.User.Identity?.Name
                         ?? httpContext?.Request.Headers["X-Client-Name"].FirstOrDefault();

    return Task.CompletedTask;
  }
}
