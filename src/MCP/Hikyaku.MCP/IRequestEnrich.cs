using MediatR;

namespace Axon.Flow.MCP;

public interface IRequestEnrich<in TRequest, TResponse>: IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
}