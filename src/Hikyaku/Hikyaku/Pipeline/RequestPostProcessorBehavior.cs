namespace Hikyaku.Pipeline;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Behavior for executing all <see cref="IRequestPostProcessor{TRequest,TResponse}"/> instances after handling the request
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public class RequestPostProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IRequestPostProcessor<TRequest, TResponse>> _postProcessors;

    /// <summary>
    /// Implements pipeline behavior for processing all registered
    /// <see cref="IRequestPostProcessor{TRequest, TResponse}"/> instances
    /// after the request handler has completed execution.
    /// </summary>

    public RequestPostProcessorBehavior(IEnumerable<IRequestPostProcessor<TRequest, TResponse>> postProcessors) 
        => _postProcessors = postProcessors;

    /// <summary>
    /// Handles pipeline execution by invoking the provided request handler delegate,
    /// and then processing the response using all registered
    /// <see cref="IRequestPostProcessor{TRequest, TResponse}"/> instances.
    /// </summary>
    /// <param name="request">The incoming request to process.</param>
    /// <param name="next">The delegate to call the next behavior or handler in the pipeline.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The processed response of type <typeparamref name="TResponse"/>.</returns>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken).ConfigureAwait(false);

        foreach (var processor in _postProcessors)
        {
            await processor.Process(request, response, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }
}