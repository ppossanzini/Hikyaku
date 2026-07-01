using MediatR;

namespace Hikyaku.Kaido.MCP;

public interface IRequestEnrich<in TRequest, TResponse>: IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
}