![Logo.png](assets/Logo-128.png) ![Logo.png](assets/Kaido 128.png) ![Logo.png](assets/Jigen 128.png)

[![NuGet](https://img.shields.io/nuget/dt/Hikyaku.svg)](https://www.nuget.org/packages/Hikyaku) 
[![NuGet](https://img.shields.io/nuget/vpre/Hikyaku.svg)](https://www.nuget.org/packages/Hikyaku)


Hikyaku is collection of open-source tools initially create as derivate work of [MediatR](https://github.com/jbogard/MediatR.Archive) version 12.5.0 
with additions to transform message dispatching from in-process to Out-Of-Process messaging via RPC calls implemented with popular message dispatchers.

The project is divided in tools
  1) Hikyaku is the base initial derivate work of MediatR with some little fixes and improvements to avoid type mismatch during pipelines. 
  2) Kaido implement RPC calls with popular message dispatchers like RabbitMQ and Kafka
  3) Jigen is a Vector Database useful in application where you need a local Vector index but you does not need enterprise level infrastructure. 
     You can consider Jigen as a counterpart of SQLite but for vector indexes. 



## When you need Hikyaku. 

Hikyaku is very good to implement some patterns like [CQRS](https://docs.microsoft.com/en-us/azure/architecture/patterns/cqrs)

Implementing CQRS in an in-process application does not bring to you all the power of the pattern but gives you the opportunity to have organized, easy to maintain code. When the application grow you may need to refactor your things to a microservices architecture.

Microservices and patterns like CQRS are very powerfull combination. In this scenario you will need to rewrite communication part to use some kind of out of process message dispatcher.

Hikyaku provides you message dispatching behaviour and let you decide which call needs to be in-process and which needs to be Out-of-process and dispatched remotely, via a configuration without changing a single row of your code.

 

## Installation

You should install [Hikyaku with NuGet](https://www.nuget.org/packages/Hikyaku)

    Install-Package Hikyaku

if you need out-of-process functions

    Install-Package Hikyaku.Kaido
    
Or via the .NET Core command line interface:

    dotnet add package Hikyaku
    dotnet add package Hikyaku.Kaido

Either commands, from Package Manager Console or .NET Core CLI, will download and install Hikyaku and all required dependencies.


## Using Contracts-Only Package
To reference only the contracts for Hikyaku, which includes:

IRequest (including generic variants)
INotification
IStreamRequest
Add a package reference to Hikyaku-Contracts

This package is useful in scenarios where your Hikyaku contracts are in a separate assembly/project from handlers. Example scenarios include:

API contracts
GRPC contracts
Blazor

## Basic Configuration using `IServiceCollection`

Configuring Hikyaku is an easy task. 
1) Add Hikyaku to services configuration via AddHikyaku extension method. this will register the Hikyaku service that can be used for message dispatching. 

Hikyaku supports `Microsoft.Extensions.DependencyInjection.Abstractions` directly. To register various Hikyaku services and handlers:

```
services.AddHikyaku(cfg => cfg.RegisterServicesFromAssemblyContaining<Startup>());
```

or with an assembly:

```
services.AddHikyaku(cfg => cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly));
```


This registers:

- `IHikyaku` as transient
- `ISender` as transient
- `IPublisher` as transient
- `IRequestHandler<,>` concrete implementations as transient
- `IRequestHandler<>` concrete implementations as transient
- `INotificationHandler<>` concrete implementations as transient
- `IStreamRequestHandler<>` concrete implementations as transient
- `IRequestExceptionHandler<,,>` concrete implementations as transient
- `IRequestExceptionAction<,>)` concrete implementations as transient

This also registers open generic implementations for:

- `INotificationHandler<>`
- `IRequestExceptionHandler<,,>`
- `IRequestExceptionAction<,>`

To register behaviors, stream behaviors, pre/post processors:

```csharp
services.AddHikyaku(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly);
    cfg.AddBehavior<PingPongBehavior>();
    cfg.AddStreamBehavior<PingPongStreamBehavior>();
    cfg.AddRequestPreProcessor<PingPreProcessor>();
    cfg.AddRequestPostProcessor<PingPongPostProcessor>();
    cfg.AddOpenBehavior(typeof(GenericBehavior<,>));
    });
```


If no configuration is given for the message dispatched, all messages are dispatched in-process.
You can change the default behaviour in using the following configuration

2) Decide what is the default behaviour, available options are 
   1) ***ImplicitLocal*** : all `hikyaku.Send()` calls will be delivered in-process unless further configuration. 
   2) ***ImplicitRemote*** : all `hikyaku.Send()` calls will be delivered out-of-process unless further configuration. 
   3) ***Explicit*** : you have the responsability do declare how to manage every single call. 


```
    services.AddKaido(opt =>
    {
      opt.Behaviour = HikyakuBehaviourEnum.Explicit;
    });
```


3) Configure calls delivery type according with you behaviour:

```
    services.AddKaido(opt =>
    {
      opt.Behaviour = HikyakuBehaviourEnum.Explicit;
      opt.SetAsRemoteRequest<Request1>();
      opt.SetAsRemoteRequest<Request2>();
      ....
    }
```

Of course you will have some processes with requests declared **Local** and other processes with same requests declared **Remote**.

### Example of process with all local calls and some remote calls

```
    services.AddKaido(opt =>
    {
      opt.Behaviour = HikyakuBehaviourEnum.ImplicitLocal;
      opt.SetAsRemoteRequest<Request1>();
      opt.SetAsRemoteRequest<Request2>();
      opt.SetAsRemoteRequests(typeof(Request2).Assembly); // All requests in an assembly
    });
```


### Example of process with local handlers. 

```
    services.AddKaido(opt =>
    {
      opt.Behaviour = HikyakuBehaviourEnum.ImplicitLocal;
    });

```

### Example of process with remore handlers. 

```
    services.AddKaido(opt =>
    {
      opt.Behaviour = HikyakuBehaviourEnum.ImplicitRemote;
    });
```


# Hikyaku with RabbitMQ


## Installing Hikyaku RabbitMQ extension.

```
    Install-Package Hikyaku.Kaido.RabbitMQ
```
    
Or via the .NET Core command line interface:

```
    dotnet add package Hikyaku.Kaido.RabbitMQ
```

## Configuring RabbitMQ Extension. 

Once installed you need to configure rabbitMQ extension. 

```
    services.AddHikyakuRabbitMQMessageDispatcher(opt =>
    {
      opt.HostName = "rabbit instance";
      opt.Port = 5672;
      opt.Password = "password";
      opt.UserName = "rabbituser";
      opt.VirtualHost = "/";
    });
    services.ResolveHikyakuCalls();
```

or if you prefer use appsettings configuration 

```
    services.AddHikyakuRabbitMQMessageDispatcher(opt => context.Configuration.GetSection("rabbitmq").Bind(opt));
    services.ResolveHikyakuCalls();
```


# Hikyaku with Kafka

## Installing Hikyaku Kafka extension.

```
    Install-Package Hikyaku.Kaido.Kafka
```
    
Or via the .NET Core command line interface:

```
    dotnet add package Hikyaku.Kaido.Kafka
```


## Configuring Kafka Extension. 

Once installed you need to configure Kafka extension. 

```
    services.AddHikyakuKafkaMessageDispatcher(opt =>
    {
      opt.BootstrapServers = "localhost:9092";
    });
    services.ResolveHikyakuCalls();
```

or if you prefer use appsettings configuration 

```
    services.AddHikyakuKafkaMessageDispatcher(opt => context.Configuration.GetSection("kafka").Bind(opt));
    services.ResolveHikyakuCalls();
```



# Hikyaku with Azure Message Queues

Coming soon. 
