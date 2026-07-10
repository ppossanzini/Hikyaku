using System.Threading;
using System.Threading.Tasks;

namespace Hikyaku.Kaido.MCP
{
  /// <summary>
  /// Enriches an MCP-originated request before it is dispatched through Hikyaku.
  /// Implementations are resolved from DI for the concrete request type, so you can write
  /// one enricher per request (closed implementation) or a cross-cutting one (open generic).
  /// Being regular DI services, enrichers can inject whatever they need to read the caller
  /// context (e.g. IHttpContextAccessor for token claims and headers), keeping handlers transport-agnostic.
  /// </summary>
  public interface IRequestEnrich<in TRequest> where TRequest :class
  {
    Task Enrich(TRequest request, CancellationToken cancellationToken);
  }
}