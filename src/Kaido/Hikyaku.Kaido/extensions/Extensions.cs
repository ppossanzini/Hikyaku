using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using Hikyaku;
using Hikyaku.Kaido;
using Microsoft.Extensions.Logging;

// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable CheckNamespace
namespace Microsoft.Extensions.DependencyInjection
{
  /// <summary>
  /// Extension methods for configuring and using Hikyaku.Router in an ASP.NET Core application.
  /// </summary>
  [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
  [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
  public static class Extensions
  {
    /// <summary>
    /// Adds the Hikyaku.Router to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection AddKaido(this IServiceCollection services, Action<RouterOptions> configure = null)
    {
      if (configure != null)
        services.Configure<RouterOptions>(configure);
      services.AddScoped(typeof(MediatR.IPipelineBehavior<,>), typeof(Hikyaku.Kaido.Pipelines.RouterPipeline<,>));
      services.AddSingleton<IRouter, global::Hikyaku.Kaido.Router>();

      services.AddTransient<global::Hikyaku.IHikyaku, Kaido>();
      return services;
    }
    


    /// <summary>
    /// Adds the Hikyaku.Router service to the specified <see cref="ServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="ServiceCollection"/> to add the Hikyaku.Router service to.</param>
    /// <param name="assemblies">The collection of assemblies to use for inference.</param>
    /// <returns>The updated <see cref="ServiceCollection"/>.</returns>
    public static IServiceCollection AddOrchestratorGateway(this ServiceCollection services, IEnumerable<Assembly> assemblies)
    {
      services.AddKaido(cfg =>
      {
        cfg.Behaviour = HikyakuBehaviourEnum.ImplicitRemote;
        cfg.InferLocalRequests(assemblies);
        cfg.InferLocalNotifications(assemblies);
      });
      return services;
    }

    /// <summary>
    /// Infers the local requests based on the provided assemblies and updates the options accordingly.
    /// </summary>
    /// <param name="options">The existing router options.</param>
    /// <param name="assemblies">The assemblies to search for request handlers.</param>
    /// <param name="queuePrefix">Prefix for Exchange and queues for requests</param>
    /// <param name="logger">Logger instance to allow information during configuration</param>
    /// <returns>The updated hikyaku options with the inferred local requests.</returns>
    public static RouterOptions InferLocalRequests(this RouterOptions options, IEnumerable<Assembly> assemblies, string queuePrefix = null,
      ILogger logger = null)
    {
      var localRequests = assemblies.SelectMany(a => a
        .GetTypes()
        .SelectMany(t => t.GetInterfaces()
          .Where(i => i.FullName != null && (i.FullName.StartsWith("Hikyaku.IRequestHandler") || i.FullName.StartsWith("MediatR.IRequestHandler")))
          .Select(i => i.GetGenericArguments()[0]).ToArray()
        ));
      options.SetAsLocalRequests(localRequests.ToArray, queuePrefix, logger);
      return options;
    }


    /// <summary>
    /// Infers local notifications for the specified <see cref="RouterOptions"/> object based on the given <paramref name="assemblies"/>.
    /// </summary>
    /// <param name="options">The <see cref="RouterOptions"/> object.</param>
    /// <param name="assemblies">The collection of <see cref="Assembly"/> objects to infer local notifications from.</param>
    /// <param name="queuePrefix">Prefix for Exchange and queues for requests</param>
    /// <param name="logger">Logger instance to allow information during configuration</param>
    /// <returns>
    /// The <see cref="RouterOptions"/> object with inferred local notifications set.
    /// </returns>
    public static RouterOptions InferLocalNotifications(this RouterOptions options, IEnumerable<Assembly> assemblies, string queuePrefix = null,
      ILogger logger = null)
    {
      var localNotifications = assemblies.SelectMany(a => a
        .GetTypes()
        .SelectMany(t => t.GetInterfaces()
          .Where(i => i.FullName != null && (i.FullName.StartsWith("Hikyaku.INotificationHandler") || i.FullName.StartsWith("MediatR.IRequestHandler")))
          .Select(i => i.GetGenericArguments()[0]).ToArray()
        ));

      options.SetAsLocalRequests(() => localNotifications, queuePrefix, logger);

      return options;
    }

    /// <summary>
    /// Sets the specified type of request as a local request in the given HikyakuOptions object.
    /// </summary>
    /// <typeparam name="T">The type of the request to set as local.</typeparam>
    /// <param name="options">The HikyakuOptions object to modify.</param>
    /// <param name="queuePrefix">Prefix for Exchange and queues for requests</param>
    /// <param name="logger">Logger instance to allow information during configuration</param>
    /// <returns>The modified HikyakuOptions object.</returns>
    public static RouterOptions SetAsLocalRequest<T>(this RouterOptions options, string queuePrefix = null, ILogger logger = null)
      where T : MediatR.IBaseRequest
    {
      options.LocalTypes.Add(typeof(T));

      if (!string.IsNullOrWhiteSpace(queuePrefix) && !options.TypePrefixes.ContainsKey(typeof(T).FullName))
      {
        options.TypePrefixes.Add(typeof(T).FullName, queuePrefix);
        logger?.LogInformation($"Added prefix to request ${typeof(T).FullName}");
      }

      return options;
    }

    /// <summary>
    /// Listens for a notification and adds it to the local requests list in the HikyakuOptions instance. </summary> <typeparam name="T">The type of the notification to listen for. It must implement the INotification interface.</typeparam> <param name="options">The HikyakuOptions instance to add the notification to.</param>
    /// <param name="queuePrefix">Prefix for Exchange and queues for requests</param>
    /// <param name="logger">Logger instance to allow information during configuration</param>
    /// <returns>The updated HikyakuOptions instance with the notification added to the local requests list.</returns>
    /// /
    public static RouterOptions ListenForNotification<T>(this RouterOptions options, string queuePrefix = null, ILogger logger = null)
      where T : MediatR.INotification
    {
      options.LocalTypes.Add(typeof(T));

      if (!string.IsNullOrWhiteSpace(queuePrefix) && !options.TypePrefixes.ContainsKey(typeof(T).FullName))
      {
        options.TypePrefixes.Add(typeof(T).FullName, queuePrefix);
        logger?.LogInformation($"Added prefix to request ${typeof(T).FullName}");
      }

      return options;
    }

    /// <summary>
    /// Sets the specified type as a remote request and adds it to the remote requests list in the <see cref="RouterOptions"/>.
    /// </summary>
    /// <typeparam name="T">The type of the remote request. It must implement the <see cref="IBaseRequest"/> interface.</typeparam>
    /// <param name="options">The <see cref="RouterOptions"/> instance to modify.</param>
    /// <param name="queuePrefix">Prefix for Exchange and queues for requests</param>
    /// <param name="logger">Logger instance to allow information during configuration</param>
    /// <returns>The modified <see cref="RouterOptions"/> instance.</returns>
    public static RouterOptions SetAsRemoteRequest<T>(this RouterOptions options, string queuePrefix = null, ILogger logger = null)
      where T : MediatR.IBaseRequest
    {
      options.RemoteTypes.Add(typeof(T));

      if (!string.IsNullOrWhiteSpace(queuePrefix) && !options.TypePrefixes.ContainsKey(typeof(T).FullName))
      {
        options.TypePrefixes.Add(typeof(T).FullName, queuePrefix);
        logger?.LogInformation($"Added prefix to request ${typeof(T).FullName}");
      }


      return options;
    }

    /// <summary>
    /// Adds selected types from the specified assemblies as local requests to the <see cref="RouterOptions"/>.
    /// </summary>
    /// <param name="options">The <see cref="RouterOptions"/> to modify.</param>
    /// <param name="assemblySelect">A function that selects the assemblies to retrieve types from.</param>
    /// <param name="queuePrefix">Prefix for Exchange and queues for requests</param>
    /// <param name="logger">Logger instance to allow information during configuration</param>
    /// <returns>The modified <see cref="RouterOptions"/>.</returns>
    public static RouterOptions SetAsLocalRequests(this RouterOptions options, Func<IEnumerable<Assembly>> assemblySelect, string queuePrefix = null,
      ILogger logger = null)
    {
      var types = (from a in assemblySelect()
        from t in a.GetTypes()
        where typeof(MediatR.IBaseRequest).IsAssignableFrom(t) || typeof(MediatR.INotification).IsAssignableFrom(t)
        select t).AsEnumerable();

      foreach (var t in types)
        options.LocalTypes.Add(t);

      if (!string.IsNullOrWhiteSpace(queuePrefix))
        foreach (var t in types)
          if (!options.TypePrefixes.ContainsKey(t.FullName))
          {
            options.TypePrefixes.Add(t.FullName, queuePrefix);
            logger?.LogInformation($"Added prefix to request ${t.FullName}");
          }

      return options;
    }

    /// <summary>
    /// Sets the specified types as local requests in the <see cref="RouterOptions"/>.
    /// </summary>
    /// <param name="options">The <see cref="RouterOptions"/> object.</param>
    /// <param name="typesSelect">A function that returns an enumerable collection of types to be set as local requests.</param>
    /// <param name="queuePrefix">Prefix for Exchange and queues for requests</param>
    /// <param name="logger">Logger instance to allow information during configuration</param>
    /// <returns>The updated <see cref="RouterOptions"/> object.</returns>
    public static RouterOptions SetAsLocalRequests(this RouterOptions options, Func<IEnumerable<Type>> typesSelect, string queuePrefix = null,
      ILogger logger = null)
    {
      foreach (var t in typesSelect())
        options.LocalTypes.Add(t);

      if (!string.IsNullOrWhiteSpace(queuePrefix))
        foreach (var t in typesSelect())
          if (!options.TypePrefixes.ContainsKey(t.FullName))
          {
            options.TypePrefixes.Add(t.FullName, queuePrefix);
            logger?.LogInformation($"Added prefix to request ${t.FullName}");
          }

      return options;
    }

    /// <summary>
    /// Sets the specified <paramref name="options"/> as remote requests.
    /// </summary>
    /// <param name="options">The <see cref="RouterOptions"/> to set as remote requests.</param>
    /// <param name="assemblySelect">The function to select the assemblies.</param>
    /// <param name="queuePrefix">Prefix for Exchange and queues for requests</param>
    /// <param name="logger">Logger instance to allow information during configuration</param>
    /// <returns>The updated <see cref="RouterOptions"/> with remote requests set.</returns>
    public static RouterOptions SetAsRemoteRequests(this RouterOptions options, Func<IEnumerable<Assembly>> assemblySelect, string queuePrefix = null,
      ILogger logger = null)
    {
      var types = (from a in assemblySelect()
        from t in a.GetTypes()
        where typeof(MediatR.IBaseRequest).IsAssignableFrom(t) || typeof(MediatR.INotification).IsAssignableFrom(t)
        select t).AsEnumerable();
      foreach (var t in types)
        options.RemoteTypes.Add(t);

      if (!string.IsNullOrWhiteSpace(queuePrefix))
        foreach (var t in types)
          if (!options.TypePrefixes.ContainsKey(t.FullName))
          {
            options.TypePrefixes.Add(t.FullName, queuePrefix);
            logger?.LogInformation($"Added prefix to request ${t.FullName}");
          }

      return options;
    }

    /// <summary>
    /// Sets the Types as remote requests.
    /// </summary>
    /// <param name="options">The HikyakuOptions object.</param>
    /// <param name="typesSelect">The function that returns IEnumerable of Type objects.</param>
    /// <param name="queuePrefix">Prefix for Exchange and queues for requests</param>
    /// <param name="logger">Logger instance to allow information during configuration</param>
    /// <returns>The modified HikyakuOptions object.</returns>
    public static RouterOptions SetAsRemoteRequests(this RouterOptions options, Func<IEnumerable<Type>> typesSelect, string queuePrefix = null,
      ILogger logger = null)
    {
      var types = typesSelect();
      if (!types.Any())
        logger?.LogWarning("SetAsRemoteRequests : No Requests classes found in assemblies");

      foreach (var t in types)
        options.RemoteTypes.Add(t);

      if (!string.IsNullOrWhiteSpace(queuePrefix))
        foreach (var t in types)
          if (!options.TypePrefixes.ContainsKey(t.FullName))
          {
            options.TypePrefixes.Add(t.FullName, queuePrefix);
            logger?.LogInformation($"Added prefix to request ${t.FullName}");
          }

      return options;
    }

    /// <summary>
    /// Set a prefix for notifications queue name.
    /// </summary>
    /// <param name="options">The HikyakuOptions object.</param>
    /// <param name="typesSelect">The function that returns IEnumerable of Type objects.</param>
    /// <param name="queuePrefix">Prefix for Exchange and queues for notification</param>
    /// <param name="logger">Logger instance to allow information during configuration</param>
    /// <returns>The modified HikyakuOptions object.</returns>
    public static RouterOptions SetNotificationPrefix(this RouterOptions options, Func<IEnumerable<Type>> typesSelect, string queuePrefix,
      ILogger logger = null)
    {
      var types = typesSelect().Where(t => typeof(MediatR.INotification).IsAssignableFrom(t));
      if (!types.Any())
        logger?.LogWarning("SetNotificationPrefix : No Notification classes found in assemblies");

      if (!string.IsNullOrWhiteSpace(queuePrefix))
        foreach (var t in types)
          if (!options.TypePrefixes.ContainsKey(t.FullName))
          {
            options.TypePrefixes.Add(t.FullName, queuePrefix);
            logger?.LogInformation($"Added prefix to notification ${t.FullName}");
          }

      return options;
    }

    public static bool IsNotification(this Type t)
    {
      return typeof(MediatR.INotification).IsAssignableFrom(t) && !typeof(MediatR.IBaseRequest).IsAssignableFrom(t);
    }

    public static RouterOptions SetTypeQueueName<T>(this RouterOptions options, string queueName)
    {
      options.SetTypeQueueName(typeof(T), queueName);
      return options;
    }

    public static RouterOptions SetTypeQueueName(this RouterOptions options, Type type, string queueName)
    {
      if (!options.QueueNames.ContainsKey(type))
        options.QueueNames.Add(type, new HashSet<string>());

      options.QueueNames[type].Add(queueName);

      return options;
    }

    public static RouterOptions SetTypesQueueName(this RouterOptions options, Func<IEnumerable<Type>> typeselect, Func<Type, string> typeNameFunction)
    {
      var types = typeselect();
      foreach (var t in types)
      {
        var result = typeNameFunction(t);
        options.SetTypeQueueName(t, result);
      }

      return options;
    }


    /// <summary>
    /// Set a prefix for notifications queue name.
    /// </summary>
    /// <param name="options">The HikyakuOptions object.</param>
    /// <param name="assemblySelect">The function to select the assemblies.</param>
    /// <param name="queuePrefix">Prefix for Exchange and queues for notification</param>
    /// <param name="logger">Logger instance to allow information during configuration</param>
    /// <returns>The modified HikyakuOptions object.</returns>
    public static RouterOptions SetNotificationPrefix(this RouterOptions options, Func<IEnumerable<Assembly>> assemblySelect, string queuePrefix,
      ILogger logger = null)
    {
      var types = (from a in assemblySelect()
        from t in a.GetTypes()
        where typeof(MediatR.INotification).IsAssignableFrom(t)
        select t).AsEnumerable();

      if (!types.Any())
        logger?.LogWarning("SetNotificationPrefix : No Notification classes found in assemblies");

      foreach (var t in types)
        if (!options.TypePrefixes.ContainsKey(t.FullName))
        {
          options.TypePrefixes.Add(t.FullName, queuePrefix);
          logger?.LogInformation($"Added prefix to notification ${t.FullName}");
        }

      return options;
    }


    /// <summary>
    /// Gets the type name used for the specified type.
    /// </summary>
    /// <param name="t">The type.</param>
    /// <param name="sb">The <see cref="StringBuilder"/> instance to append the type name to (optional).</param>
    /// <returns>The type name for the specified type.</returns>
    public static string AxonTypeName(this Type t, RouterOptions options, StringBuilder sb = null)
    {
      if (t.CustomAttributes.Any())
      {
        var attr = t.GetCustomAttribute<RouterQueueNameAttribute>();
        if (attr != null) return attr.Absolute ? attr.Name : $"{t.Namespace}.{attr.Name}".Replace(".", "_");
      }

      options.TypePrefixes.TryGetValue(t.FullName, out var prefix);
      prefix = prefix ?? options.DefaultQueuePrefix;

      sb = sb ?? new StringBuilder();

      if (!string.IsNullOrWhiteSpace(prefix)) sb.Append($"{prefix}.");
      sb.Append($"{t.Namespace}.{t.Name}");

      if (t.GenericTypeArguments != null && t.GenericTypeArguments.Length > 0)
      {
        sb.Append("[");
        foreach (var ta in t.GenericTypeArguments)
        {
          ta.AxonTypeName(options, sb);
          sb.Append(",");
        }

        sb.Append("]");
      }

      return sb.ToString().Replace(",]", "]").Replace(".", "_");
    }


    public static int? QueueTimeout(this Type t)
    {
      if (t.CustomAttributes.Any())
      {
        var attr = t.GetCustomAttribute<RouterQueueTimeoutAttribute>();
        if (attr != null) return attr.ConsumerTimeout;
      }

      return null;
    }
  }
}