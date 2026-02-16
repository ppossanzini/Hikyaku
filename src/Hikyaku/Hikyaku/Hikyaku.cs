using Hikyaku.NotificationPublishers;


namespace Hikyaku;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wrappers;

/// <summary>
/// Default orchestrator implementation relying on single- and multi instance delegates for resolving handlers.
/// </summary>
public class Hikyaku : IHikyaku
{
  private readonly IServiceProvider _serviceProvider;
  private readonly INotificationPublisher _publisher;
  private static readonly ConcurrentDictionary<Type, RequestHandlerBase> _requestHandlers = new();
  private static readonly ConcurrentDictionary<Type, NotificationHandlerWrapper> _notificationHandlers = new();
  private static readonly ConcurrentDictionary<Type, StreamRequestHandlerBase> _streamRequestHandlers = new();

  /// <summary>
  /// Initializes a new instance of the <see cref="Hikyaku"/> class.
  /// </summary>
  /// <param name="serviceProvider">Service provider. Can be a scoped or root provider</param>
  public Hikyaku(IServiceProvider serviceProvider)
    : this(serviceProvider, new ForeachAwaitPublisher())
  {
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="Hikyaku"/> class.
  /// </summary>
  /// <param name="serviceProvider">Service provider. Can be a scoped or root provider</param>
  /// <param name="publisher">Notification publisher. Defaults to <see cref="ForeachAwaitPublisher"/>.</param>
  public Hikyaku(IServiceProvider serviceProvider, INotificationPublisher publisher)
  {
    _serviceProvider = serviceProvider;
    _publisher = publisher;
  }

  /// <summary>
  /// Sends a request to the appropriate handler and returns a response of the specified type.
  /// </summary>
  /// <typeparam name="TResponse">The type of the response expected from the request handler.</typeparam>
  /// <param name="request">The request to be processed. Cannot be null.</param>
  /// <param name="cancellationToken">A cancellation token to observe while waiting for the request to be handled.</param>
  /// <returns>A task representing the asynchronous operation, containing the response from the request handler.</returns>
  public Task<TResponse> Send<TResponse>(IRequest<TResponse> request,
    CancellationToken cancellationToken = default)
  {
    if (request == null)
    {
      throw new ArgumentNullException(nameof(request));
    }

    var handler = (RequestHandlerWrapper<TResponse>)_requestHandlers.GetOrAdd(request.GetType(), static requestType =>
    {
      var wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, typeof(TResponse));
      var wrapper = Activator.CreateInstance(wrapperType) ??
                    throw new InvalidOperationException($"Could not create wrapper type for {requestType}");
      return (RequestHandlerBase)wrapper;
    });

    return handler.Handle(request, _serviceProvider, cancellationToken);
  }


  /// <summary>
  /// Sends a request to the appropriate handler and returns a task representing the asynchronous operation.
  /// </summary>
  /// <typeparam name="TRequest">The type of the request being sent. Must implement the <see cref="IRequest"/> interface.</typeparam>
  /// <param name="request">The request to process. Must not be null.</param>
  /// <param name="cancellationToken">A token that can be used to signal the cancellation of the operation.</param>
  /// <returns>A task that represents the asynchronous operation of handling the request.</returns>
  /// <exception cref="ArgumentNullException">Thrown if the request is null.</exception>
  /// <exception cref="InvalidOperationException">Thrown if no handler could be created for the request type.</exception>
  public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
    where TRequest : IRequest
  {
    if (request == null)
    {
      throw new ArgumentNullException(nameof(request));
    }

    var handler = (RequestHandlerWrapper)_requestHandlers.GetOrAdd(request.GetType(), static requestType =>
    {
      var wrapperType = typeof(RequestHandlerWrapperImpl<>).MakeGenericType(requestType);
      var wrapper = Activator.CreateInstance(wrapperType) ??
                    throw new InvalidOperationException($"Could not create wrapper type for {requestType}");
      return (RequestHandlerBase)wrapper;
    });

    return handler.Handle(request, _serviceProvider, cancellationToken);
  }

  /// <summary>
  /// Sends the specified request object to the appropriate handler for processing asynchronously.
  /// </summary>
  /// <param name="request">The request object to be processed. Must implement <see cref="IRequest"/> or <see cref="IRequest{TResponse}"/>.</param>
  /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
  /// <returns>A task representing the asynchronous operation. The result of the task is the response from the handler or null if no response is expected.</returns>
  public Task<object?> Send(object request, CancellationToken cancellationToken = default)
  {
    return SendObject(request, cancellationToken);
  }

  /// <summary>
  /// Sends the specified request object to the appropriate handler for processing asynchronously.
  /// </summary>
  /// <param name="request">The request object to be processed. Must implement <see cref="IRequest"/> or <see cref="IRequest{TResponse}"/>.</param>
  /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
  /// <returns>A task representing the asynchronous operation. The result of the task is the response from the handler or null if no response is expected.</returns>
  public Task<object?> SendObject(object request, CancellationToken cancellationToken = default)
  {
    if (request == null)
    {
      throw new ArgumentNullException(nameof(request));
    }

    var handler = _requestHandlers.GetOrAdd(request.GetType(), static requestType =>
    {
      Type wrapperType;

      var requestInterfaceType = requestType.GetInterfaces().FirstOrDefault(static i =>
        i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));
      if (requestInterfaceType is null)
      {
        requestInterfaceType = requestType.GetInterfaces().FirstOrDefault(static i => i == typeof(IRequest));
        if (requestInterfaceType is null)
        {
          throw new ArgumentException($"{requestType.Name} does not implement {nameof(IRequest)}",
            nameof(request));
        }

        wrapperType = typeof(RequestHandlerWrapperImpl<>).MakeGenericType(requestType);
      }
      else
      {
        var responseType = requestInterfaceType.GetGenericArguments()[0];
        wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, responseType);
      }

      var wrapper = Activator.CreateInstance(wrapperType) ??
                    throw new InvalidOperationException($"Could not create wrapper for type {requestType}");
      return (RequestHandlerBase)wrapper;
    });

    // call via dynamic dispatch to avoid calling through reflection for performance reasons
    return handler.Handle(request, _serviceProvider, cancellationToken);
  }


  /// <summary>
  /// Publishes an object as a notification.
  /// </summary>
  /// <param name="notification">The object to be published. Must implement <see cref="INotification"/>.</param>
  /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
  /// <returns>A task that represents the asynchronous publish operation.</returns>
  /// <exception cref="ArgumentNullException">Thrown when the <paramref name="notification"/> parameter is null.</exception>
  /// <exception cref="ArgumentException">Thrown when the <paramref name="notification"/> does not implement <see cref="INotification"/>.</exception>
  public Task Publish(object notification, CancellationToken cancellationToken = default)
  {
    return PublishObject(notification, cancellationToken);
  }

  /// <summary>
  /// Publishes a notification to all registered handlers.
  /// </summary>
  /// <typeparam name="TNotification">The type of the notification being published. Must implement <see cref="INotification"/>.</typeparam>
  /// <param name="notification">The notification instance to be published.</param>
  /// <param name="cancellationToken">Token to cancel the publish operation.</param>
  /// <returns>A task that represents the asynchronous publish operation.</returns>
  /// <exception cref="ArgumentNullException">Thrown when the notification is null.</exception>
  public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
    where TNotification : INotification
  {
    if (notification == null)
    {
      throw new ArgumentNullException(nameof(notification));
    }

    return PublishNotification(notification, cancellationToken);
  }

  /// <summary>
  /// Publishes an object as a notification.
  /// </summary>
  /// <param name="notification">The object to be published. Must implement <see cref="INotification"/>.</param>
  /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
  /// <returns>A task that represents the asynchronous publish operation.</returns>
  /// <exception cref="ArgumentNullException">Thrown when the <paramref name="notification"/> parameter is null.</exception>
  /// <exception cref="ArgumentException">Thrown when the <paramref name="notification"/> does not implement <see cref="INotification"/>.</exception>
  public Task PublishObject(object notification, CancellationToken cancellationToken = default) =>
    notification switch
    {
      null => throw new ArgumentNullException(nameof(notification)),
      INotification instance => PublishNotification(instance, cancellationToken),
      _ => throw new ArgumentException($"{nameof(notification)} does not implement ${nameof(INotification)}")
    };

  /// <summary>
  /// Override in a derived class to control how the tasks are awaited. By default the implementation calls the <see cref="INotificationPublisher"/>.
  /// </summary>
  /// <param name="handlerExecutors">Enumerable of tasks representing invoking each notification handler</param>
  /// <param name="notification">The notification being published</param>
  /// <param name="cancellationToken">The cancellation token</param>
  /// <returns>A task representing invoking all handlers</returns>
  protected virtual Task PublishCore(IEnumerable<NotificationHandlerExecutor> handlerExecutors,
    INotification notification, CancellationToken cancellationToken)
    => _publisher.Publish(handlerExecutors, notification, cancellationToken);

  private Task PublishNotification(INotification notification, CancellationToken cancellationToken = default)
  {
    var handler = _notificationHandlers.GetOrAdd(notification.GetType(), static notificationType =>
    {
      var wrapperType = typeof(NotificationHandlerWrapperImpl<>).MakeGenericType(notificationType);
      var wrapper = Activator.CreateInstance(wrapperType) ??
                    throw new InvalidOperationException($"Could not create wrapper for type {notificationType}");
      return (NotificationHandlerWrapper)wrapper;
    });

    return handler.Handle(notification, _serviceProvider, PublishCore, cancellationToken);
  }


  /// <summary>
  /// Creates an asynchronous stream of responses for the specified stream request.
  /// </summary>
  /// <typeparam name="TResponse">The type of the response element in the stream.</typeparam>
  /// <param name="request">The stream request to process.</param>
  /// <param name="cancellationToken">A token for observing cancellation requests.</param>
  /// <returns>An asynchronous stream of responses provided by the stream request handler.</returns>
  public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request,
    CancellationToken cancellationToken = default)
  {
    if (request == null)
    {
      throw new ArgumentNullException(nameof(request));
    }

    var streamHandler = (StreamRequestHandlerWrapper<TResponse>)_streamRequestHandlers.GetOrAdd(request.GetType(),
      static requestType =>
      {
        var wrapperType = typeof(StreamRequestHandlerWrapperImpl<,>).MakeGenericType(requestType, typeof(TResponse));
        var wrapper = Activator.CreateInstance(wrapperType) ??
                      throw new InvalidOperationException($"Could not create wrapper for type {requestType}");
        return (StreamRequestHandlerBase)wrapper;
      });

    var items = streamHandler.Handle(request, _serviceProvider, cancellationToken);

    return items;
  }


  /// <summary>
  /// Creates an asynchronous stream of results based on the specified request.
  /// </summary>
  /// <param name="request">The request object to process, which must implement <see cref="IStreamRequest{TResponse}"/>.</param>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation. This parameter is optional.</param>
  /// <returns>An asynchronous enumerable that streams the results of the operation.</returns>
  /// <exception cref="ArgumentNullException">Thrown when the <paramref name="request"/> is null.</exception>
  /// <exception cref="ArgumentException">Thrown when the <paramref name="request"/> does not implement <see cref="IStreamRequest{TResponse}"/>.</exception>
  /// <exception cref="InvalidOperationException">Thrown when an appropriate handler for the request cannot be created.</exception>
  public IAsyncEnumerable<object?> CreateStream(object request,
    CancellationToken cancellationToken = default)
  {
    return CreateStreamObject(request, cancellationToken);
  }

  /// <summary>
  /// Creates an asynchronous stream of results based on the specified request.
  /// </summary>
  /// <param name="request">The request object to process, which must implement <see cref="IStreamRequest{TResponse}"/>.</param>
  /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation. This parameter is optional.</param>
  /// <returns>An asynchronous enumerable that streams the results of the operation.</returns>
  /// <exception cref="ArgumentNullException">Thrown when the <paramref name="request"/> is null.</exception>
  /// <exception cref="ArgumentException">Thrown when the <paramref name="request"/> does not implement <see cref="IStreamRequest{TResponse}"/>.</exception>
  /// <exception cref="InvalidOperationException">Thrown when an appropriate handler for the request cannot be created.</exception>
  public IAsyncEnumerable<object?> CreateStreamObject(object request, CancellationToken cancellationToken = default)
  {
    if (request == null)
    {
      throw new ArgumentNullException(nameof(request));
    }

    var handler = _streamRequestHandlers.GetOrAdd(request.GetType(), static requestType =>
    {
      var requestInterfaceType = requestType.GetInterfaces()
        .FirstOrDefault(static i =>
          i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStreamRequest<>));
      if (requestInterfaceType is null)
      {
        throw new ArgumentException($"{requestType.Name} does not implement IStreamRequest<TResponse>",
          nameof(request));
      }

      var responseType = requestInterfaceType.GetGenericArguments()[0];
      var wrapperType = typeof(StreamRequestHandlerWrapperImpl<,>).MakeGenericType(requestType, responseType);
      var wrapper = Activator.CreateInstance(wrapperType) ??
                    throw new InvalidOperationException($"Could not create wrapper for type {requestType}");
      return (StreamRequestHandlerBase)wrapper;
    });

    var items = handler.Handle(request, _serviceProvider, cancellationToken);

    return items;
  }
}