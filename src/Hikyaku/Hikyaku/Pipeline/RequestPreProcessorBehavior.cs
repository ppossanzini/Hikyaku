namespace Hikyaku.Pipeline;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Behavior for executing all <see cref="IRequestPreProcessor{TRequest}"/> instances before handling a request
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
public class RequestPreProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IRequestPreProcessor<TRequest>> _preProcessors;

    /// <summary>
    /// Implements the pipeline behavior for executing all request pre-processors
    /// associated with a request before the handler is invoked.
    /// </summary>

    public RequestPreProcessorBehavior(IEnumerable<IRequestPreProcessor<TRequest>> preProcessors) 
        => _preProcessors = preProcessors;

    /// <summary>
    /// Handles the execution of a request through the pipeline, including all pre-processors,
    /// and invokes the next delegate in the pipeline.
    /// </summary>
    /// <param name="request">The request object being processed.</param>
    /// <param name="next">The delegate representing the next action in the pipeline.</param>
    /// <param name="cancellationToken">A token used to propagate notification that operations should be canceled.</param>
    /// <returns>The response resulting from processing the request.</returns>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        foreach (var processor in _preProcessors)
        {
            await processor.Process(request, cancellationToken).ConfigureAwait(false);
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }
}