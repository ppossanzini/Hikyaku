namespace Hikyaku.Pipeline;

using Internal;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Behavior for executing all <see cref="IRequestExceptionAction{TRequest,TException}"/> instances
///     after an exception is thrown by the following pipeline steps
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public class RequestExceptionActionProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Represents a pipeline behavior that processes exception actions
    /// when an exception occurs during the execution of subsequent pipeline steps.
    /// </summary>
    public RequestExceptionActionProcessorBehavior(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    /// <summary>
    /// Handles the execution of pipeline behaviors for a given request and response, including
    /// executing exception actions when exceptions are thrown during the request handling process.
    /// </summary>
    /// <param name="request">The request being processed.</param>
    /// <param name="next">The delegate representing the next step in the pipeline.</param>
    /// <param name="cancellationToken">Token to propagate notification that the operation should be canceled.</param>
    /// <returns>The response resulting from executing the pipeline behaviors and the request handler.</returns>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var exceptionTypes = GetExceptionTypes(exception.GetType());

            var actionsForException = exceptionTypes
                .SelectMany(exceptionType => GetActionsForException(exceptionType, request))
                .GroupBy(static actionForException => actionForException.Action.GetType())
                .Select(static actionForException => actionForException.First())
                .Select(static actionForException => (MethodInfo: GetMethodInfoForAction(actionForException.ExceptionType), actionForException.Action))
                .ToList();

            foreach (var actionForException in actionsForException)
            {
                try
                {
                    await ((Task)(actionForException.MethodInfo.Invoke(actionForException.Action, new object[] { request, exception, cancellationToken })
                                  ?? throw new InvalidOperationException($"Could not create task for action method {actionForException.MethodInfo}."))).ConfigureAwait(false);
                }
                catch (TargetInvocationException invocationException) when (invocationException.InnerException != null)
                {
                    // Unwrap invocation exception to throw the actual error
                    ExceptionDispatchInfo.Capture(invocationException.InnerException).Throw();
                }
            }

            throw;
        }
    }

    private static IEnumerable<Type> GetExceptionTypes(Type? exceptionType)
    {
        while (exceptionType != null && exceptionType != typeof(object))
        {
            yield return exceptionType;
            exceptionType = exceptionType.BaseType;
        }
    }

    private IEnumerable<(Type ExceptionType, object Action)> GetActionsForException(Type exceptionType, TRequest request)
    {
        var exceptionActionInterfaceType = typeof(IRequestExceptionAction<,>).MakeGenericType(typeof(TRequest), exceptionType);
        var enumerableExceptionActionInterfaceType = typeof(IEnumerable<>).MakeGenericType(exceptionActionInterfaceType);

        var actionsForException = (IEnumerable<object>)_serviceProvider.GetRequiredService(enumerableExceptionActionInterfaceType);

        return HandlersOrderer.Prioritize(actionsForException.ToList(), request)
            .Select(action => (exceptionType, action));
    }

    private static MethodInfo GetMethodInfoForAction(Type exceptionType)
    {
        var exceptionActionInterfaceType = typeof(IRequestExceptionAction<,>).MakeGenericType(typeof(TRequest), exceptionType);

        var actionMethodInfo =
            exceptionActionInterfaceType.GetMethod(nameof(IRequestExceptionAction<TRequest, Exception>.Execute))
            ?? throw new InvalidOperationException(
                $"Could not find method {nameof(IRequestExceptionAction<TRequest, Exception>.Execute)} on type {exceptionActionInterfaceType}");

        return actionMethodInfo;
    }
}
