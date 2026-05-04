# AGENTS.md

## Architecture Overview
Hikyaku is a .NET library derived from MediatR, enabling both in-process and out-of-process request/notification handling via RPC. The core `Hikyaku` class orchestrates message dispatching, while `Kaido` extends it for remote routing using message dispatchers (RabbitMQ, Kafka, gRPC). Components are modular: `Hikyaku` (core), `Hikyaku.Contracts` (interfaces), `Hikyaku.Kaido` (routing), dispatcher packages (e.g., `Hikyaku.Kaido.RabbitMQ`), and `Axon.Flow.MCP` (AI agent integration via Model Context Protocol).

Data flows: Requests/notifications enter via `IHikyaku.Send()` or `Publish()`. The `Router` determines local vs. remote based on `RouterOptions.Behaviour` (ImplicitLocal/ImplicitRemote/Explicit). Local calls use DI-resolved handlers; remote calls dispatch via `IExternalMessageDispatcher` (e.g., RabbitMQ for RPC). Notifications fan out to all handlers, locally or remotely.

Service boundaries: Core library handles orchestration; Kaido manages routing; dispatchers handle transport; MCP exposes resources/tools to AI agents.

## Key Workflows
- **Build**: `dotnet build Hikyaku.sln` (standard .NET solution).
- **Test**: `dotnet test` (if test projects exist; none visible in src/).
- **Debug**: Use IDE (Visual Studio/Rider) for local debugging; inspect logs for remote dispatches.
- **Remote Setup**: Configure dispatchers (e.g., RabbitMQ host/port in `AddHikyakuRabbitMQMessageDispatcher`); set `RouterOptions.Behaviour` and register local/remote types explicitly.

## Project Conventions
- **Registration**: Use `services.AddHikyaku(cfg => cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly))` for assembly scanning. Handlers/behaviors registered as transient.
- **Handler Naming**: Implement `IRequestHandler<TRequest, TResponse>` or `INotificationHandler<TNotification>`. Example: `public class MyRequestHandler : IRequestHandler<MyRequest, MyResponse> { ... }`.
- **Pipelines**: Add behaviors via `cfg.AddBehavior<MyBehavior>()`; supports pre/post processors and stream behaviors.
- **Remote Config**: For Explicit mode, use `opt.SetAsRemoteRequest<MyRequest>()`. Dispatchers registered with keyed service `"OrchestratorMessageDispatchers"`.
- **Contracts**: Define requests/notifications in `Hikyaku.Contracts` assembly for cross-project sharing.
- **MCP Integration**: Use `[AgentResource]` or `[AgentTool]` attributes in `Axon.Flow.MCP` to expose endpoints to AI agents.

## Integration Points
- **Message Dispatchers**: Implement `IExternalMessageDispatcher` for custom transports (e.g., Azure Queues). Existing: RabbitMQ (`AddHikyakuRabbitMQMessageDispatcher`), Kafka (`AddHikyakuKafkaMessageDispatcher`), gRPC.
- **External Dependencies**: RabbitMQ/Kafka clients, StreamJsonRpc for MCP.
- **Cross-Component Communication**: Use `IRouteTo` interface for routing hints; `ResolveHikyakuCalls()` to finalize remote setups.

Reference: `src/Hikyaku/Hikyaku/Hikyaku.cs` (core dispatcher), `src/Kaido/Hikyaku.Kaido/Router.cs` (routing logic), `README.md` (setup examples).</content>
<parameter name="filePath">/home/paolo/Work/archer/axonflow/AGENTS.md
