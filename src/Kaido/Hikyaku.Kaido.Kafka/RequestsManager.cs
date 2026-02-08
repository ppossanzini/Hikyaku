using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Hikyaku.Kaido.Messages;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Hikyaku.Kaido.Kafka
{
  /// <summary>
  /// Represents a class that manages requests and notifications using Kafka.
  /// </summary>
  public class RequestsManager : IHostedService
  {
    private readonly ILogger<RequestsManager> _logger;
    private readonly IRouter _router;
    private readonly IServiceProvider _provider;
    private readonly RouterOptions _hikyakuOptions;

    private readonly MessageDispatcherOptions _options;
    private IProducer<Null, string> _producer;
    private IConsumer<Null, string> _requestConsumer;
    private IConsumer<Null, string> _notificationConsumer;

    private Thread _notificationConsumerThread;
    private Thread _requestConsumerThread;

    private bool _running = true;

    private readonly Dictionary<string, MethodInfo> _methods = new Dictionary<string, MethodInfo>();

    public RequestsManager(ILogger<RequestsManager> logger, IOptions<MessageDispatcherOptions> options, IRouter router, IServiceProvider provider,
      IOptions<RouterOptions> hikyakuOptions)
    {
      _logger = logger;
      _options = options.Value;
      this._router = router;
      this._provider = provider;
      _hikyakuOptions = hikyakuOptions.Value;
    }

    /// <summary>
    /// Starts the asynchronous process of connecting to Kafka and subscribing to messages.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
      _producer = new ProducerBuilder<Null, string>(this._options.GetProducerConfig()).Build();
      _logger.LogInformation($"Creating Kafka Connection to '{_options.BootstrapServers}'...");

      {
        var config = this._options.GetConsumerConfig();
        config.GroupId = "Hikyaku.Router";
        _requestConsumer = new ConsumerBuilder<Null, string>(config).Build();
      }

      {
        var config = this._options.GetConsumerConfig();
        config.GroupId = $"Notification.{Process.GetCurrentProcess().Id}.{DateTime.Now.Ticks}";
        _notificationConsumer = new ConsumerBuilder<Null, string>(config).Build();
      }

      var notificationsSubscriptions = new List<string>();
      var requestSubscriptions = new List<string>();

      foreach (var t in _router.GetLocalRequestsTypes())
      {
        if (t is null) continue;
        _provider.CreateTopicAsync(_options, t.AxonTypeName(_hikyakuOptions));


        if (t.IsNotification())
        {
          notificationsSubscriptions.Add(t.AxonTypeName(_hikyakuOptions));
          var consumerMethod = typeof(RequestsManager)
            .GetMethod("ConsumeChannelNotification", BindingFlags.Instance | BindingFlags.NonPublic)?
            .MakeGenericMethod(t);
          _methods.Add(t.AxonTypeName(_hikyakuOptions), consumerMethod);
        }
        else
        {
          requestSubscriptions.Add(t.AxonTypeName(_hikyakuOptions));
          var consumerMethod = typeof(RequestsManager)
            .GetMethod("ConsumeChannelMessage", BindingFlags.Instance | BindingFlags.NonPublic)?
            .MakeGenericMethod(t);
          _methods.Add(t.AxonTypeName(_hikyakuOptions), consumerMethod);
        }
      }

      _requestConsumer.Subscribe(requestSubscriptions);
      _notificationConsumer.Subscribe(notificationsSubscriptions);

      _requestConsumerThread = new Thread(() =>
      {
        while (_running)
        {
          var notification = _requestConsumer.Consume();
          _methods.TryGetValue(notification.Topic, out var method);
          if (method != null)
            method.Invoke(this, new object[] { notification.Message.Value });
        }
      }) { IsBackground = true };
      _requestConsumerThread.Start();

      _notificationConsumerThread = new Thread(() =>
      {
        while (_running)
        {
          var notification = _notificationConsumer.Consume();
          _methods.TryGetValue(notification.Topic, out var method);
          if (method != null)
            method.Invoke(this, new object[] { notification.Message.Value });
        }
      }) { IsBackground = true };

      _notificationConsumerThread.Start();


      return Task.CompletedTask;
    }

    /// <summary>
    /// Elaborates and processes a notification received from a channel. </summary> <typeparam name="T">The type of the message.</typeparam> <param name="msg">The notification message to be processed.</param> <returns>A task representing the asynchronous operation.</returns>
    /// /
    private async Task ConsumeChannelNotification<T>(string msg)
    {
      _logger.LogDebug("Elaborating notification : {Msg}", msg);
      var message = JsonConvert.DeserializeObject<KafkaMessage<T>>(msg, _options.SerializerSettings);
      if (message == null)
      {
        _logger.LogError("Unable to deserialize message {Msg}", msg);
        return;
      }

      var axon = _provider.CreateScope().ServiceProvider.GetRequiredService<IHikyaku>();
      try
      {
        var hikyaku = axon as Kaido;
        hikyaku?.StopPropagating();
        await axon.PublishObject(message.Message);
        hikyaku?.ResetPropagating();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, $"Error executing message of type {typeof(T)} from external service");
      }
    }

    /// <summary>
    /// Consume a channel message and process it.
    /// </summary>
    /// <typeparam name="T">Type of the message content.</typeparam>
    /// <param name="msg">The message to consume.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ConsumeChannelMessage<T>(string msg)
    {
      _logger.LogDebug("Elaborating message : {Msg}", msg);
      var message = JsonConvert.DeserializeObject<KafkaMessage<T>>(msg, _options.SerializerSettings);

      if (message == null)
      {
        _logger.LogError("Unable to deserialize message {Msg}", msg);
        return;
      }

      var axon = _provider.CreateScope().ServiceProvider.GetRequiredService<IHikyaku>();
      string responseMsg = null;
      try
      {
        var response = await axon.SendObject(message.Message);
        responseMsg = JsonConvert.SerializeObject(
          new KafkaReply
          {
            Reply = new ResponseMessage { Content = response, Status = StatusEnum.Ok },
            CorrelationId = message.CorrelationId
          }, _options.SerializerSettings);
        _logger.LogDebug("Elaborating sending response : {Msg}", responseMsg);
      }
      catch (Exception ex)
      {
        responseMsg = JsonConvert.SerializeObject(
          new KafkaReply()
          {
            Reply = new ResponseMessage
            {
              Exception = ex,
              OriginaStackTrace = ex.StackTrace?.ToString(),
              Status = StatusEnum.Exception, Content = MediatR.Unit.Value
            },
            CorrelationId = message.CorrelationId
          }
          , _options.SerializerSettings);
        _logger.LogError(ex, $"Error executing message of type {typeof(T)} from external service");
      }
      finally
      {
        await _producer.ProduceAsync(message.ReplyTo, new Message<Null, string>() { Value = responseMsg });
      }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
      _running = false;
      return Task.CompletedTask;
    }
  }
}