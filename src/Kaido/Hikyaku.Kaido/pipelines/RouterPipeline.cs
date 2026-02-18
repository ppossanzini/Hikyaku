using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hikyaku.Kaido.Pipelines
{
  /// <summary>
  /// Represents a pipeline behavior that integrates with an arbitrator for handling requests of type <typeparamref name="TRequest"/> and producing responses of type <typeparamref name
  /// ="TResponse"/>.
  /// </summary>
  /// <typeparam name="TRequest">The type of the request.</typeparam>
  /// <typeparam name="TResponse">The type of the response.</typeparam>
  public class RouterPipeline<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class, IBaseRequest
  //where TRequest : notnull
  {
    private readonly IRouter _router;
    private readonly ILogger<Router> _logger;
    private readonly IServiceProvider _provider;

    public RouterPipeline(IRouter router, ILogger<Router> logger, IServiceProvider provider)
    {
      this._router = router;
      _logger = logger;
      this._provider = provider;
    }


    private async Task<TResponse> InvokeHandler<T>(RequestHandlerDelegate<TResponse> next, T req,
      CancellationToken cancellationToken)
    {
      var performanceMonitor = _provider.GetService<IPerformanceMonitor>();

      try
      {
        switch (_router.GetLocation(req.GetType()))
        {
          case HandlerLocation.Local:
          {
            performanceMonitor?.NewLocalRequest(typeof(T));
            var result = await next(cancellationToken).ConfigureAwait(false);
            performanceMonitor?.SuccessfullyCompleted(typeof(T));
            return result;
          }
          case HandlerLocation.Remote:
          {
            performanceMonitor?.NewRemoteRequest(typeof(T));
            var result = await _router.InvokeRemoteHandler<T, TResponse>(req);
            performanceMonitor?.SuccessfullyCompleted(typeof(T));
            return result;
          }
          default: throw new InvalidHandlerException();
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, ex.Message);
        performanceMonitor?.CompletedWithExceptions(typeof(T));
        throw;
      }
    }


    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
      CancellationToken cancellationToken)
    {
      if (typeof(IExplicitQueue).IsAssignableFrom(request.GetType()))
      {
        var queueName = ((IExplicitQueue)request).QueueName;
        var req = ((IExplicitQueue)request).MessageObject;
        var type = request.GetType().GetGenericArguments()[0];

        return ((TResponse)GetType().GetMethod(nameof(InvokeHandler), BindingFlags.Instance | BindingFlags.NonPublic)
          ?.MakeGenericMethod(type)
          .Invoke(this, new[] { next, req, queueName, cancellationToken }));
      }

      return await InvokeHandler(next, request, cancellationToken);
    }
  }
}