namespace Hikyaku.Wrappers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Defines an abstract base class for wrapping and handling notifications within a system.
/// </summary>
public abstract class NotificationHandlerWrapper
{
  /// <summary>
  /// Handles the processing of a given notification using the provided service factory,
  /// notification handler executors, and cancellation token.
  /// </summary>
  /// <param name="notification">The notification instance to be handled.</param>
  /// <param name="serviceFactory">The service provider factory responsible for resolving services.</param>
  /// <param name="publish">
  /// A function delegate to publish the notification to the appropriate notification handler executors.
  /// </param>
  /// <param name="cancellationToken">A token for cancelling the operation if required.</param>
  /// <returns>A task that represents the asynchronous handling operation.</returns>
  public abstract Task Handle(INotification notification, IServiceProvider serviceFactory,
    Func<IEnumerable<NotificationHandlerExecutor>, INotification, CancellationToken, Task> publish,
    CancellationToken cancellationToken);
}

/// <summary>
/// Implements the <see cref="NotificationHandlerWrapper"/> to provide specific handling logic
/// for a given notification type by utilizing registered handlers.
/// </summary>
/// <typeparam name="TNotification">
/// The type of notification that this wrapper is designed to handle.
/// Must implement the <see cref="INotification"/> interface.
/// </typeparam>
public class NotificationHandlerWrapperImpl<TNotification> : NotificationHandlerWrapper
  where TNotification : INotification
{
  /// <summary>
  /// Handles the processing of a given notification by invoking the appropriate notification handlers
  /// retrieved through the provided service factory, using the specified publishing function.
  /// </summary>
  /// <param name="notification">The notification to be handled.</param>
  /// <param name="serviceFactory">The factory used to resolve the notification handler services.</param>
  /// <param name="publish">
  /// A function delegate that publishes the notification to a collection of notification handler executors.
  /// </param>
  /// <param name="cancellationToken">A token that propagates notification that the operation should be canceled.</param>
  /// <returns>A task representing the asynchronous handling operation.</returns>
  public override Task Handle(INotification notification, IServiceProvider serviceFactory,
    Func<IEnumerable<NotificationHandlerExecutor>, INotification, CancellationToken, Task> publish,
    CancellationToken cancellationToken)
  {
    var handlers =
      serviceFactory.GetServices<INotificationHandler<TNotification>>()
        
        .Select(static x => new NotificationHandlerExecutor(x,
          (theNotification, theToken) => x.Handle((TNotification)theNotification, theToken)));

    return publish(handlers, notification, cancellationToken);
  }
}