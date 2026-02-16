using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Hikyaku.Wrappers;

/// <summary>
/// Represents the base class for request handlers.
/// </summary>
public abstract class RequestHandlerBase
{
  /// <summary>
  /// Handles a request by delegating to the appropriate handler.
  /// </summary>
  /// <param name="request">The request object to handle.</param>
  /// <param name="serviceProvider">The service provider used to resolve dependencies required during request handling.</param>
  /// <param name="cancellationToken">A token to observe cancellation requests.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the response object, or null if no response is returned.</returns>
  public abstract Task<object?> Handle(object request, IServiceProvider serviceProvider,
    CancellationToken cancellationToken);
}

/// <summary>
/// Represents an abstract wrapper for request handlers that handle specific response types.
/// </summary>
/// <typeparam name="TResponse">The type of the response returned by the handler.</typeparam>
public abstract class RequestHandlerWrapper<TResponse> : RequestHandlerBase
{
  /// <summary>
  /// Processes a request by resolving its corresponding handler and executing it.
  /// </summary>
  /// <param name="request">The request object to be processed.</param>
  /// <param name="serviceProvider">The service provider used to resolve dependencies needed by the handler.</param>
  /// <param name="cancellationToken">A token to monitor for cancellation requests during request processing.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the response of the request.</returns>
  public abstract Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider,
    CancellationToken cancellationToken);
}

/// <summary>
/// Represents an abstract wrapper for request handlers that handle requests without specific response types.
/// </summary>
public abstract class RequestHandlerWrapper : RequestHandlerBase
{
  /// <summary>
  /// Handles a request by delegating to the appropriate handler wrapper.
  /// </summary>
  /// <param name="request">The request object to be handled.</param>
  /// <param name="serviceProvider">The service provider used to resolve dependencies required during request handling.</param>
  /// <param name="cancellationToken">A token to observe cancellation requests.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains an instance of Unit, indicating the completion of the handling process.</returns>
  public abstract Task<Unit> Handle(IRequest request, IServiceProvider serviceProvider,
    CancellationToken cancellationToken);
}

/// <summary>
/// Represents the implementation of a wrapper for request handlers that processes requests
/// of a specific type and produces responses of a specific type.
/// </summary>
/// <typeparam name="TRequest">The type of the request handled by this wrapper.</typeparam>
/// <typeparam name="TResponse">The type of the response produced by this wrapper.</typeparam>
public class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
  where TRequest : IRequest<TResponse>
{
  /// <summary>
  /// Handles a request by delegating it to the appropriate service and processing any attached pipeline behaviors.
  /// </summary>
  /// <param name="request">The request object to be handled.</param>
  /// <param name="serviceProvider">A service provider to resolve the required dependencies such as handlers and pipeline behaviors.</param>
  /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete or to cancel the operation.</param>
  /// <returns>A task that represents the asynchronous handling operation. The task result contains the response object or null if no response is provided.</returns>
  public override async Task<object?> Handle(object request, IServiceProvider serviceProvider,
    CancellationToken cancellationToken) =>
    await Handle((IRequest<TResponse>)request, serviceProvider, cancellationToken).ConfigureAwait(false);

  /// <summary>
  /// Handles the provided request asynchronously using the specified service provider and cancellation token,
  /// invoking pipeline behaviors and the request handler as required.
  /// </summary>
  /// <param name="request">The request object to be handled, implementing <see cref="IRequest{TResponse}"/>.</param>
  /// <param name="serviceProvider">The service provider used to resolve pipeline behaviors and the appropriate request handler.</param>
  /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
  /// <returns>A task representing the asynchronous operation, with the result being the response object of type <typeparamref name="TResponse"/>.</returns>
  public override Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider,
    CancellationToken cancellationToken)
  {
    Task<TResponse> Handler(CancellationToken t = default) =>
      (serviceProvider.GetService<IRequestHandler<TRequest, TResponse>>() ??
       serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>())
      .Handle((TRequest)request, t == default ? cancellationToken : t);

    return serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>()
      .Reverse()
      .Aggregate((RequestHandlerDelegate<TResponse>)Handler,
        (next, pipeline) => (t) => pipeline.Handle((TRequest)request, next, t == default ? cancellationToken : t))();
  }
}

/// <summary>
/// Represents a concrete implementation of the request handler wrapper
/// for processing requests without defined response types.
/// </summary>
public class RequestHandlerWrapperImpl<TRequest> : RequestHandlerWrapper
  where TRequest : IRequest
{
  /// <summary>
  /// Handles a request by invoking the appropriate handler logic with the provided request and service dependencies.
  /// </summary>
  /// <param name="request">The request object to process.</param>
  /// <param name="serviceProvider">The service provider used to resolve any required dependencies for handling the request.</param>
  /// <param name="cancellationToken">A token used to observe cancellation requests during the operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the response object, or null if no result is produced.</returns>
  public override async Task<object?> Handle(object request, IServiceProvider serviceProvider,
    CancellationToken cancellationToken) =>
    await Handle((IRequest)request, serviceProvider, cancellationToken).ConfigureAwait(false);

  /// <summary>
  /// Handles a request by executing the configured request handling process
  /// and invoking the pipeline behaviors in reverse order before reaching the main handler.
  /// </summary>
  /// <param name="request">The request object to process.</param>
  /// <param name="serviceProvider">The service provider used to resolve dependencies such as the handler and pipeline behaviors.</param>
  /// <param name="cancellationToken">A token to observe cancellation requests during the handling process.</param>
  /// <returns>A task that represents the asynchronous operation. The task result is a unit indicating successful completion of the process.</returns>
  public override Task<Unit> Handle(IRequest request, IServiceProvider serviceProvider,
    CancellationToken cancellationToken)
  {
    async Task<Unit> Handler(CancellationToken t = default)
    {
      await (serviceProvider.GetService<IRequestHandler<TRequest>>() ??
             serviceProvider.GetRequiredService<IRequestHandler<TRequest>>())
        .Handle((TRequest)request, t == default ? cancellationToken : t);

      return Unit.Value;
    }

    return serviceProvider.GetServices<IPipelineBehavior<TRequest, Unit>>()
      .Reverse()
      .Aggregate((RequestHandlerDelegate<Unit>)Handler,
        (next, pipeline) => (t) => pipeline.Handle((TRequest)request, next, t == default ? cancellationToken : t))();
  }
}