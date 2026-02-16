using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Hikyaku.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hikyaku.Registration;

/// <summary>
/// Provides methods for registering services and managing the configuration
/// necessary for Hikyaku-based dependency injection setups.
/// </summary>
public static class ServiceRegistrar
{
  private static int MaxGenericTypeParameters;
  private static int MaxTypesClosing;
  private static int MaxGenericTypeRegistrations;
  private static int RegistrationTimeout;

  /// <summary>
  /// Sets limitations for generic request handler registration based on the specified configuration values.
  /// </summary>
  /// <param name="configuration">
  /// An instance of <see cref="HikyakuServiceConfiguration"/> that specifies the constraints for generic request handler registration,
  /// including maximum generic type parameters, maximum types closing, maximum generic type registrations,
  /// and registration timeout values.
  /// </param>
  public static void SetGenericRequestHandlerRegistrationLimitations(HikyakuServiceConfiguration configuration)
  {
    MaxGenericTypeParameters = configuration.MaxGenericTypeParameters;
    MaxTypesClosing = configuration.MaxTypesClosing;
    MaxGenericTypeRegistrations = configuration.MaxGenericTypeRegistrations;
    RegistrationTimeout = configuration.RegistrationTimeout;
  }

  /// <summary>
  /// Registers MediatR-related classes into the service collection with a specified timeout configuration.
  /// </summary>
  /// <param name="services">
  /// The <see cref="IServiceCollection"/> where MediatR-related services should be added.
  /// </param>
  /// <param name="configuration">
  /// An instance of <see cref="HikyakuServiceConfiguration"/> that provides the configuration settings
  /// for service registration, including lifetime, type evaluator, and other constraints.
  /// </param>
  [Obsolete("Use AddHikyakuClassesWithTimeout instead", false)]
  public static void AddMediatRClassesWithTimeout(IServiceCollection services, HikyakuServiceConfiguration configuration)
  {
    AddHikyakuClassesWithTimeout(services, configuration); 
  }

  /// <summary>
  /// Registers Hikyaku classes with a set timeout to prevent long-running operations during registration.
  /// </summary>
  /// <param name="services">
  /// An instance of <see cref="IServiceCollection"/> that holds the service descriptors to be registered.
  /// </param>
  /// <param name="configuration">
  /// An instance of <see cref="HikyakuServiceConfiguration"/> that provides necessary registration settings and preferences.
  /// </param>
  /// <exception cref="TimeoutException">
  /// Thrown when the registration process exceeds the specified timeout duration.
  /// </exception>
  public static void AddHikyakuClassesWithTimeout(IServiceCollection services, HikyakuServiceConfiguration configuration)
  {
    using (var cts = new CancellationTokenSource(RegistrationTimeout))
    {
      try
      {
        AddHikyakuClasses(services, configuration, cts.Token);
      }
      catch (OperationCanceledException)
      {
        throw new TimeoutException("The generic handler registration process timed out.");
      }
    }
  }

  /// <summary>
  /// Registers MediatR-related services and configurations using the provided service collection,
  /// configuration, and optional cancellation token.
  /// </summary>
  /// <param name="services">
  /// The dependency injection service collection where MediatR-related services will be registered.
  /// </param>
  /// <param name="configuration">
  /// An instance of <see cref="HikyakuServiceConfiguration"/> that contains the necessary settings
  /// for configuring MediatR-related registrations, such as service lifetimes, request handlers, and publishers.
  /// </param>
  /// <param name="cancellationToken">
  /// An optional <see cref="CancellationToken"/> used to observe and handle cancellation during the registration process.
  /// Defaults to <see cref="CancellationToken.None"/> if not provided.
  /// </param>
  [Obsolete("Use AddHikyakuClasses instead", false)]
  public static void AddMediatRClasses(IServiceCollection services, HikyakuServiceConfiguration configuration,
    CancellationToken cancellationToken = default)
  {
    AddHikyakuClasses(services, configuration, cancellationToken);
  }

  /// <summary>
  /// Registers Hikyaku-specific classes into the provided dependency injection service collection based on the specified configuration and cancellation token.
  /// </summary>
  /// <param name="services">
  /// An instance of <see cref="IServiceCollection"/> where the Hikyaku-specific classes will be registered.
  /// </param>
  /// <param name="configuration">
  /// An instance of <see cref="HikyakuServiceConfiguration"/> that provides configuration options such as assemblies to scan and type evaluation logic.
  /// </param>
  /// <param name="cancellationToken">
  /// A token that can be used to signal the cancellation of the registration process.
  /// </param>
  public static void AddHikyakuClasses(IServiceCollection services, HikyakuServiceConfiguration configuration, CancellationToken cancellationToken = default)
  {
    var assembliesToScan = configuration.AssembliesToRegister.Distinct().ToArray();

    ConnectImplementationsToTypesClosing(typeof(IRequestHandler<,>), services, assembliesToScan, false, configuration, cancellationToken);
    ConnectImplementationsToTypesClosing(typeof(IRequestHandler<>), services, assembliesToScan, false, configuration, cancellationToken);
    ConnectImplementationsToTypesClosing(typeof(INotificationHandler<>), services, assembliesToScan, true, configuration, cancellationToken);
    ConnectImplementationsToTypesClosing(typeof(IStreamRequestHandler<,>), services, assembliesToScan, false, configuration, cancellationToken);
    ConnectImplementationsToTypesClosing(typeof(IRequestExceptionHandler<,,>), services, assembliesToScan, true, configuration, cancellationToken);
    ConnectImplementationsToTypesClosing(typeof(IRequestExceptionAction<,>), services, assembliesToScan, true, configuration, cancellationToken);

    if (configuration.AutoRegisterRequestProcessors)
    {
      ConnectImplementationsToTypesClosing(typeof(IRequestPreProcessor<>), services, assembliesToScan, true, configuration, cancellationToken);
      ConnectImplementationsToTypesClosing(typeof(IRequestPostProcessor<,>), services, assembliesToScan, true, configuration, cancellationToken);
    }

    var multiOpenInterfaces = new List<Type>
    {
      typeof(INotificationHandler<>),
      typeof(IRequestExceptionHandler<,,>),
      typeof(IRequestExceptionAction<,>)
    };

    if (configuration.AutoRegisterRequestProcessors)
    {
      multiOpenInterfaces.Add(typeof(IRequestPreProcessor<>));
      multiOpenInterfaces.Add(typeof(IRequestPostProcessor<,>));
    }

    foreach (var multiOpenInterface in multiOpenInterfaces)
    {
      var arity = multiOpenInterface.GetGenericArguments().Length;

      var concretions = assembliesToScan
        .SelectMany(a => a.DefinedTypes)
        .Where(type => type.FindInterfacesThatClose(multiOpenInterface).Any())
        .Where(type => type.IsConcrete() && type.IsOpenGeneric())
        .Where(type => type.GetGenericArguments().Length == arity)
        .Where(configuration.TypeEvaluator)
        .ToList();

      foreach (var type in concretions)
      {
        services.AddTransient(multiOpenInterface, type);
      }
    }
  }

  private static void ConnectImplementationsToTypesClosing(Type openRequestInterface,
    IServiceCollection services,
    IEnumerable<Assembly> assembliesToScan,
    bool addIfAlreadyExists,
    HikyakuServiceConfiguration configuration,
    CancellationToken cancellationToken = default)
  {
    var concretions = new List<Type>();
    var interfaces = new List<Type>();
    var genericConcretions = new List<Type>();
    var genericInterfaces = new List<Type>();

    var types = assembliesToScan
      .SelectMany(a => a.DefinedTypes)
      .Where(t => !t.ContainsGenericParameters || configuration.RegisterGenericHandlers)
      .Where(t => t.IsConcrete() && t.FindInterfacesThatClose(openRequestInterface).Any())
      .Where(configuration.TypeEvaluator)
      .ToList();

    foreach (var type in types)
    {
      var interfaceTypes = type.FindInterfacesThatClose(openRequestInterface).ToArray();

      if (!type.IsOpenGeneric())
      {
        concretions.Add(type);

        foreach (var interfaceType in interfaceTypes)
        {
          interfaces.Fill(interfaceType);
        }
      }
      else
      {
        genericConcretions.Add(type);
        foreach (var interfaceType in interfaceTypes)
        {
          genericInterfaces.Fill(interfaceType);
        }
      }
    }

    foreach (var @interface in interfaces)
    {
      var exactMatches = concretions.Where(x => x.CanBeCastTo(@interface)).ToList();
      if (addIfAlreadyExists)
      {
        foreach (var type in exactMatches)
        {
          services.AddTransient(@interface, type);
        }
      }
      else
      {
        if (exactMatches.Count > 1)
        {
          exactMatches.RemoveAll(m => !IsMatchingWithInterface(m, @interface));
        }

        foreach (var type in exactMatches)
        {
          services.TryAddTransient(@interface, type);
        }
      }

      if (!@interface.IsOpenGeneric())
      {
        AddConcretionsThatCouldBeClosed(@interface, concretions, services);
      }
    }

    foreach (var @interface in genericInterfaces)
    {
      var exactMatches = genericConcretions.Where(x => x.CanBeCastTo(@interface)).ToList();
      AddAllConcretionsThatClose(@interface, exactMatches, services, assembliesToScan, cancellationToken);
    }
  }

  private static bool IsMatchingWithInterface(Type? handlerType, Type handlerInterface)
  {
    if (handlerType == null || handlerInterface == null)
    {
      return false;
    }

    if (handlerType.IsInterface)
    {
      if (handlerType.GenericTypeArguments.SequenceEqual(handlerInterface.GenericTypeArguments))
      {
        return true;
      }
    }
    else
    {
      return IsMatchingWithInterface(handlerType.GetInterface(handlerInterface.Name), handlerInterface);
    }

    return false;
  }

  private static void AddConcretionsThatCouldBeClosed(Type @interface, List<Type> concretions, IServiceCollection services)
  {
    foreach (var type in concretions
               .Where(x => x.IsOpenGeneric() && x.CouldCloseTo(@interface)))
    {
      try
      {
        services.TryAddTransient(@interface, type.MakeGenericType(@interface.GenericTypeArguments));
      }
      catch (Exception)
      {
      }
    }
  }

  private static (Type Service, Type Implementation) GetConcreteRegistrationTypes(Type openRequestHandlerInterface, Type concreteGenericTRequest,
    Type openRequestHandlerImplementation)
  {
    var closingTypes = concreteGenericTRequest.GetGenericArguments();

    var concreteTResponse = concreteGenericTRequest.GetInterfaces()
      .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IRequest<>))
      ?.GetGenericArguments()
      .FirstOrDefault();

    var typeDefinition = openRequestHandlerInterface.GetGenericTypeDefinition();

    var serviceType = concreteTResponse != null
      ? typeDefinition.MakeGenericType(concreteGenericTRequest, concreteTResponse)
      : typeDefinition.MakeGenericType(concreteGenericTRequest);

    return (serviceType, openRequestHandlerImplementation.MakeGenericType(closingTypes));
  }

  private static List<Type>? GetConcreteRequestTypes(Type openRequestHandlerInterface, Type openRequestHandlerImplementation,
    IEnumerable<Assembly> assembliesToScan, CancellationToken cancellationToken)
  {
    //request generic type constraints       
    var constraintsForEachParameter = openRequestHandlerImplementation
      .GetGenericArguments()
      .Select(x => x.GetGenericParameterConstraints())
      .ToList();

    var typesThatCanCloseForEachParameter = constraintsForEachParameter
      .Select(constraints => assembliesToScan
        .SelectMany(assembly => assembly.GetTypes())
        .Where(type => type.IsClass && !type.IsAbstract && constraints.All(constraint => constraint.IsAssignableFrom(type))).ToList()
      ).ToList();

    var requestType = openRequestHandlerInterface.GenericTypeArguments.First();

    if (requestType.IsGenericParameter)
      return null;

    var requestGenericTypeDefinition = requestType.GetGenericTypeDefinition();

    var combinations = GenerateCombinations(requestType, typesThatCanCloseForEachParameter, 0, cancellationToken);

    return combinations.Select(types => requestGenericTypeDefinition.MakeGenericType(types.ToArray())).ToList();
  }

  // Method to generate combinations recursively
  /// <summary>
  /// Generates all possible combinations of types from the provided lists, enforcing constraints and supporting cancellation.
  /// </summary>
  /// <param name="requestType">
  /// The generic request type for which combinations of types are being generated.
  /// </param>
  /// <param name="lists">
  /// A list of type lists, where each inner list contains types that can close the generic parameters of the request type.
  /// </param>
  /// <param name="depth">
  /// The current recursive depth in the combination generation process (default is 0).
  /// </param>
  /// <param name="cancellationToken">
  /// A token to observe for cancellation requests, allowing the operation to be terminated prematurely.
  /// </param>
  /// <returns>
  /// A list of combinations, where each combination is a list of types that can close the generic parameters of the request type.
  /// </returns>
  /// <exception cref="ArgumentException">
  /// Thrown if the number of generic type parameters exceeds the maximum allowed or if one of the lists has more types than the maximum allowed.
  /// </exception>
  /// <exception cref="OperationCanceledException">
  /// Thrown if the cancellation is requested before or during the combination generation process.
  /// </exception>
  public static List<List<Type>> GenerateCombinations(Type requestType, List<List<Type>> lists, int depth = 0, CancellationToken cancellationToken = default)
  {
    if (depth == 0)
    {
      // Initial checks
      if (MaxGenericTypeParameters > 0 && lists.Count > MaxGenericTypeParameters)
        throw new ArgumentException(
          $"Error registering the generic type: {requestType.FullName}. The number of generic type parameters exceeds the maximum allowed ({MaxGenericTypeParameters}).");

      foreach (var list in lists)
      {
        if (MaxTypesClosing > 0 && list.Count > MaxTypesClosing)
          throw new ArgumentException(
            $"Error registering the generic type: {requestType.FullName}. One of the generic type parameter's count of types that can close exceeds the maximum length allowed ({MaxTypesClosing}).");
      }

      // Calculate the total number of combinations
      long totalCombinations = 1;
      foreach (var list in lists)
      {
        totalCombinations *= list.Count;
        if (MaxGenericTypeParameters > 0 && totalCombinations > MaxGenericTypeRegistrations)
          throw new ArgumentException(
            $"Error registering the generic type: {requestType.FullName}. The total number of generic type registrations exceeds the maximum allowed ({MaxGenericTypeRegistrations}).");
      }
    }

    if (depth >= lists.Count)
      return new List<List<Type>> { new List<Type>() };

    cancellationToken.ThrowIfCancellationRequested();

    var currentList = lists[depth];
    var childCombinations = GenerateCombinations(requestType, lists, depth + 1, cancellationToken);
    var combinations = new List<List<Type>>();

    foreach (var item in currentList)
    {
      foreach (var childCombination in childCombinations)
      {
        var currentCombination = new List<Type> { item };
        currentCombination.AddRange(childCombination);
        combinations.Add(currentCombination);
      }
    }

    return combinations;
  }

  private static void AddAllConcretionsThatClose(Type openRequestInterface, List<Type> concretions, IServiceCollection services,
    IEnumerable<Assembly> assembliesToScan, CancellationToken cancellationToken)
  {
    foreach (var concretion in concretions)
    {
      var concreteRequests = GetConcreteRequestTypes(openRequestInterface, concretion, assembliesToScan, cancellationToken);

      if (concreteRequests is null)
        continue;

      var registrationTypes = concreteRequests
        .Select(concreteRequest => GetConcreteRegistrationTypes(openRequestInterface, concreteRequest, concretion));

      foreach (var (Service, Implementation) in registrationTypes)
      {
        cancellationToken.ThrowIfCancellationRequested();
        services.AddTransient(Service, Implementation);
      }
    }
  }

  internal static bool CouldCloseTo(this Type openConcretion, Type closedInterface)
  {
    var openInterface = closedInterface.GetGenericTypeDefinition();
    var arguments = closedInterface.GenericTypeArguments;

    var concreteArguments = openConcretion.GenericTypeArguments;
    return arguments.Length == concreteArguments.Length && openConcretion.CanBeCastTo(openInterface);
  }

  private static bool CanBeCastTo(this Type pluggedType, Type pluginType)
  {
    if (pluggedType == null) return false;

    if (pluggedType == pluginType) return true;

    return pluginType.IsAssignableFrom(pluggedType);
  }

  private static bool IsOpenGeneric(this Type type)
  {
    return type.IsGenericTypeDefinition || type.ContainsGenericParameters;
  }

  internal static IEnumerable<Type> FindInterfacesThatClose(this Type pluggedType, Type templateType)
  {
    return FindInterfacesThatClosesCore(pluggedType, templateType).Distinct();
  }

  private static IEnumerable<Type> FindInterfacesThatClosesCore(Type pluggedType, Type templateType)
  {
    if (pluggedType == null) yield break;

    if (!pluggedType.IsConcrete()) yield break;

    if (templateType.IsInterface)
    {
      foreach (
        var interfaceType in
        pluggedType.GetInterfaces()
          .Where(type => type.IsGenericType && (type.GetGenericTypeDefinition() == templateType)))
      {
        yield return interfaceType;
      }
    }
    else if (pluggedType.BaseType!.IsGenericType &&
             (pluggedType.BaseType!.GetGenericTypeDefinition() == templateType))
    {
      yield return pluggedType.BaseType!;
    }

    if (pluggedType.BaseType == typeof(object)) yield break;

    foreach (var interfaceType in FindInterfacesThatClosesCore(pluggedType.BaseType!, templateType))
    {
      yield return interfaceType;
    }
  }

  private static bool IsConcrete(this Type type)
  {
    return !type.IsAbstract && !type.IsInterface;
  }

  private static void Fill<T>(this IList<T> list, T value)
  {
    if (list.Contains(value)) return;
    list.Add(value);
  }

  /// <summary>
  /// Adds and configures required Hikyaku services for dependency injection based on the provided service configuration.
  /// </summary>
  /// <param name="services">
  /// An instance of <see cref="IServiceCollection"/> used to register services in the dependency injection container.
  /// </param>
  /// <param name="serviceConfiguration">
  /// An instance of <see cref="HikyakuServiceConfiguration"/> specifying the settings and types to register,
  /// including orchestrator implementations, notification publishers, and request/response behaviors.
  /// </param>
  public static void AddRequiredServices(IServiceCollection services, HikyakuServiceConfiguration serviceConfiguration)
  {
    // Use TryAdd, so any existing ServiceFactory/IOrchestrator registration doesn't get overridden
    services.TryAdd(new ServiceDescriptor(typeof(IHikyaku), serviceConfiguration.OrchestratorImplementationType, serviceConfiguration.Lifetime));
    // services.TryAdd(new ServiceDescriptor(typeof(IMediator), serviceConfiguration.OrchestratorImplementationType, serviceConfiguration.Lifetime));
    services.TryAdd(new ServiceDescriptor(typeof(ISender), sp => sp.GetRequiredService<IHikyaku>(), serviceConfiguration.Lifetime));
    services.TryAdd(new ServiceDescriptor(typeof(IPublisher), sp => sp.GetRequiredService<IHikyaku>(), serviceConfiguration.Lifetime));
    services.TryAdd(new ServiceDescriptor(typeof(ISender), sp => sp.GetRequiredService<IHikyaku>(), serviceConfiguration.Lifetime));
    services.TryAdd(new ServiceDescriptor(typeof(IPublisher), sp => sp.GetRequiredService<IHikyaku>(), serviceConfiguration.Lifetime));
    
    var notificationPublisherServiceDescriptor = serviceConfiguration.NotificationPublisherType != null
      ? new ServiceDescriptor(typeof(INotificationPublisher), serviceConfiguration.NotificationPublisherType, serviceConfiguration.Lifetime)
      : new ServiceDescriptor(typeof(INotificationPublisher), serviceConfiguration.NotificationPublisher);

    services.TryAdd(notificationPublisherServiceDescriptor);

    // Register pre processors, then post processors, then behaviors
    if (serviceConfiguration.RequestExceptionActionProcessorStrategy == RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions)
    {
      RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionActionProcessorBehavior<,>), typeof(IRequestExceptionAction<,>));
      RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionProcessorBehavior<,>), typeof(IRequestExceptionHandler<,,>));
    }
    else
    {
      RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionProcessorBehavior<,>), typeof(IRequestExceptionHandler<,,>));
      RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionActionProcessorBehavior<,>), typeof(IRequestExceptionAction<,>));
    }

    if (serviceConfiguration.RequestPreProcessorsToRegister.Any())
    {
      services.TryAddEnumerable(new ServiceDescriptor(typeof(IPipelineBehavior<,>), typeof(RequestPreProcessorBehavior<,>), ServiceLifetime.Transient));
      services.TryAddEnumerable(serviceConfiguration.RequestPreProcessorsToRegister);
    }

    if (serviceConfiguration.RequestPostProcessorsToRegister.Any())
    {
      services.TryAddEnumerable(new ServiceDescriptor(typeof(IPipelineBehavior<,>), typeof(RequestPostProcessorBehavior<,>), ServiceLifetime.Transient));
      services.TryAddEnumerable(serviceConfiguration.RequestPostProcessorsToRegister);
    }

    foreach (var serviceDescriptor in serviceConfiguration.BehaviorsToRegister)
    {
      services.TryAddEnumerable(serviceDescriptor);
    }

    foreach (var serviceDescriptor in serviceConfiguration.StreamBehaviorsToRegister)
    {
      services.TryAddEnumerable(serviceDescriptor);
    }
  }

  private static void RegisterBehaviorIfImplementationsExist(IServiceCollection services, Type behaviorType, Type subBehaviorType)
  {
    var hasAnyRegistrationsOfSubBehaviorType = services
      .Where(service => !service.IsKeyedService)
      .Select(service => service.ImplementationType)
      .OfType<Type>()
      .SelectMany(type => type.GetInterfaces())
      .Where(type => type.IsGenericType)
      .Select(type => type.GetGenericTypeDefinition())
      .Any(type => type == subBehaviorType);

    if (hasAnyRegistrationsOfSubBehaviorType)
    {
      services.TryAddEnumerable(new ServiceDescriptor(typeof(IPipelineBehavior<,>), behaviorType, ServiceLifetime.Transient));
    }
  }
}