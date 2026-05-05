using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hikyaku.Kaido.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Hikyaku.Kaido.RabbitMQ
{
  /// <summary>
  /// The RequestsManager class is responsible for managing requests and notifications in a distributed system. It implements the IHostedService interface.
  /// </summary>
  public class RequestsManager : IHostedService
  {
    private RouterOptions _routerOptions;

    /// <summary>
    /// Represents the logger for the RequestsManager class.
    /// </summary>
    /// <typeparam name="RequestsManager">The type of the class using the logger.</typeparam>
    private readonly ILogger<RequestsManager> _logger;

    /// <summary>
    /// The private readonly field that holds an instance of the IHikyaku interface.
    /// </summary>
    private readonly IRouter _router;

    /// <summary>
    /// Represents a service provider.
    /// </summary>
    private readonly IServiceProvider _provider;

    /// <summary>
    /// Represents a private instance of an IConnection object.
    /// </summary>
    private IConnection _connection;

    /// <summary>
    /// Represents the channel used for communication.
    /// </summary>
    // private IChannel _channel;
    private Dictionary<Type, IChannel> _channels = new Dictionary<Type, IChannel>();

    private Dictionary<Type, AsyncEventingBasicConsumer> _consumers = new Dictionary<Type, AsyncEventingBasicConsumer>();


    /// <summary>
    /// Represents the options for the message dispatcher.
    /// </summary>
    private readonly MessageDispatcherOptions _options;

    /// <summary>
    /// Constructs a new instance of the RequestsManager class.
    /// </summary>
    /// <param name="options">The options for the message dispatcher.</param>
    /// <param name="logger">The logger to be used for logging.</param>
    /// <param name="router">The object responsible for coordinating requests.</param>
    /// <param name="provider">The service provider for resolving dependencies.</param>
    public RequestsManager(IOptions<MessageDispatcherOptions> options, ILogger<RequestsManager> logger, IRouter router, IServiceProvider provider,
      IOptions<RouterOptions> routerOptions)
    {
      this._routerOptions = routerOptions.Value;
      this._options = options.Value;
      this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this._router = router;
      this._provider = provider;
    }

    /// <summary>
    /// Starts the asynchronous process of connecting to RabbitMQ and consuming messages from queues.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
      await CheckConnection(cancellationToken);

      await CheckRequestsConsumers(cancellationToken);

      foreach (var channel in _channels.Values)
      {
        await ValidateConnectionQos(channel, cancellationToken);
      }
    }


    private async Task CheckRequestsConsumers(CancellationToken cancellationToken)
    {
      foreach (var t in _router.GetLocalRequestsTypes())
      {
        if (t is null) continue;
        var isNotification = t.IsNotification();
        var isDurableNotification = isNotification && _routerOptions.QueueNames.ContainsKey(t);
        var queueNames = t.HikyakuQueueName(_routerOptions);

        var arguments = new Dictionary<string, object>();
        var timeout = t.QueueTimeout();
        if (timeout != null)
        {
          arguments.Add("x-consumer-timeout", timeout);
          //   arguments: new Dictionary<string, object>
          // {
          //   { "x-message-ttl", 60000 },
          //   { "x-dead-letter-exchange", $"{_options.ExchangeName}.dlx" }
          // });
        }


        var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        _channels.Add(t, channel);

        foreach (var queueName in queueNames)
        {
          await channel.QueueDeclareAsync(queue: queueName, durable: _options.Durable,
            exclusive: isNotification && !isDurableNotification,
            autoDelete: _options.AutoDelete, arguments: arguments, cancellationToken: cancellationToken);
          await channel.QueueBindAsync(queueName, _options.ExchangeName, queueName.Split('$')[0], cancellationToken: cancellationToken);
        }

        var consumer = new AsyncEventingBasicConsumer(channel);
        _consumers.Add(t, consumer);

        var consumerMethod = typeof(RequestsManager)
          .GetMethod(isNotification ? nameof(ConsumeChannelNotification) : nameof(ConsumeChannelMessage), BindingFlags.Instance | BindingFlags.NonPublic)?
          .MakeGenericMethod(t);


        consumer.ReceivedAsync += async (s, ea) =>
        {
          try
          {
            if (consumerMethod != null)
              await (Task)consumerMethod.Invoke(this, new object[] { s, ea });
          }
          catch (Exception e)
          {
            _logger.LogError(e, e.Message);
          }
        };
        foreach (var queueName in queueNames)
        {
          await channel.BasicConsumeAsync(queue: queueName, autoAck: isNotification, consumer: consumer, cancellationToken: cancellationToken);
        }
      }
    }

    private async Task ValidateConnectionQos(IChannel channel, CancellationToken cancellationToken)
    {
      try
      {
        if (_options.PerChannelQos == 0)
        {
          var maxMessages =  Math.Min(_options.PerConsumerQos , ushort.MaxValue);
          _logger.LogInformation($"Configuring Qos for channels with: prefetch = 0 and fetch size = {maxMessages}");
          await channel.BasicQosAsync(0, maxMessages, true, cancellationToken: cancellationToken);
        }
        else
        {
          await channel.BasicQosAsync(0, _options.PerChannelQos > ushort.MaxValue ? ushort.MaxValue : (ushort)_options.PerChannelQos, true,
            cancellationToken: cancellationToken);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError("Current RabbitMQ does not support Qos for channels");
        _logger.LogError(ex.Message);
        _logger.LogError(ex.StackTrace);
      }

      try
      {
        _logger.LogInformation($"Configuring Qos for consumers with: prefetch = 0 and fetch size = {Math.Max(_options.PerConsumerQos, (ushort)1)}");
        await channel.BasicQosAsync(0, Math.Max(_options.PerConsumerQos, (ushort)1), false, cancellationToken: cancellationToken);
      }
      catch (Exception ex)
      {
        _logger.LogError("Current RabbitMQ does not support Qos for consumers");
        _logger.LogError(ex.Message);
        _logger.LogError(ex.StackTrace);
      }
    }

    private async Task CheckConnection(CancellationToken cancellationToken)
    {
      if (_connection != null && _connection.IsOpen)
      {
        _connection = null;
      }

      if (_connection == null)
      {
        _logger.LogInformation($"Hikyaku: Creating RabbitMQ Connection to '{_options.HostName}'...");
        var factory = new ConnectionFactory
        {
          HostName = _options.HostName,
          UserName = _options.UserName,
          Password = _options.Password,
          VirtualHost = _options.VirtualHost,
          Port = _options.Port,
          MaxInboundMessageBodySize = _options.MaxMessageSize,
          ClientProvidedName = _options.ClientName
        };

        factory.AutomaticRecoveryEnabled = true;
        factory.NetworkRecoveryInterval = TimeSpan.FromSeconds(10);
        factory.TopologyRecoveryEnabled = true;

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        // _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        // await _channel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Topic, cancellationToken: cancellationToken);

        _logger.LogInformation("Hikyaku: ready !");
      }
    }


    /// <summary>
    /// ConsumeChannelNotification is a private asynchronous method that handles the consumption of channel notifications. </summary>
    /// <typeparam name="T">The type of messages to be consumed</typeparam> <param name="sender">The object that triggered the event</param> <param name="ea">The event arguments containing the consumed message</param>
    /// <returns>A Task representing the asynchronous operation</returns>
    /// /
    private async Task ConsumeChannelNotification<T>(object _, BasicDeliverEventArgs ea)
    {
      var axon = _provider.CreateScope().ServiceProvider.GetRequiredService<IHikyaku>();
      var hikyaku = axon as Kaido;
      try
      {
        var msg = ea.Body.ToArray();

        _logger.LogDebug("Elaborating notification : {0}", Encoding.UTF8.GetString(msg));
        var message = JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(msg), _options.SerializerSettings);

        hikyaku?.StopPropagating();
        await axon.PublishObject(message);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, $"Error executing message of type {typeof(T)} from external service");
      }
      finally
      {
        hikyaku?.ResetPropagating();
      }
    }

    /// <summary>
    /// Consumes a message from a channel and processes it asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the message being consumed.</typeparam>
    /// <param name="sender">The object that raised the event.</param>
    /// <param name="ea">An object that contains the event data.</param>
    /// <returns>A task representing the asynchronous processing of the message.</returns>
    /// <remarks>
    /// This method deserializes the message using the specified <c>DeserializerSettings</c>,
    /// sends it to the mediator for processing, and publishes a response message to the
    /// specified reply-to queue. If an exception occurs during processing, an error response
    /// message will be published.
    /// </remarks>
    private async Task ConsumeChannelMessage<T>(object _, BasicDeliverEventArgs ea)
    {
      string responseMsg = null;
      if (! _channels.TryGetValue(typeof(T), out var channel) || channel == null)
        _logger.LogError($"Cannot find the channel for message of type {typeof(T)}. Message will be acknowledged but not processed");


      var replyProps = new BasicProperties();
      try
      {
        replyProps.CorrelationId = ea.BasicProperties.CorrelationId;

        var msg = ea.Body.ToArray();
        _logger.LogDebug("Elaborating message : {0}", Encoding.UTF8.GetString(msg));
        var message = JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(msg), _options.SerializerSettings);

        var axon = _provider.CreateScope().ServiceProvider.GetRequiredService<IHikyaku>();
        var response = await axon.SendObject(message);
        responseMsg = JsonConvert.SerializeObject(new ResponseMessage { Content = response, Status = StatusEnum.Ok },
          _options.SerializerSettings);
        _logger.LogDebug("Elaborating sending response : {0}", responseMsg);
      }
      catch (Exception ex)
      {
        responseMsg = JsonConvert.SerializeObject(new ResponseMessage
          {
            Exception = ex,
            OriginaStackTrace = ex.StackTrace?.ToString(),
            Status = StatusEnum.Exception, Content = Unit.Value
          },
          _options.SerializerSettings);
        _logger.LogError(ex, $"Error executing message of type {typeof(T)} from external service");
      }
      finally
      {
        await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);

        if (!string.IsNullOrWhiteSpace(ea.BasicProperties.ReplyTo))
          await channel.BasicPublishAsync(exchange: "", routingKey: ea.BasicProperties.ReplyTo, basicProperties: replyProps,
            body: Encoding.UTF8.GetBytes(responseMsg ?? ""),
            mandatory: false); // cannot be mandatory.. if the replyTo queue is not present, we cannot do anything about it, and we don't want to lose the message in this case
      }
    }

    /// <summary>
    /// Stops the asynchronous operation and closes the channel and connection.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
      try
      {
        foreach (var channel in _channels.Values.Where(c => c != null))
        {
          await channel.CloseAsync(cancellationToken);
        }

        // if (_channel != null)
        //   await _channel.CloseAsync(cancellationToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error closing RabbitMQ channels");
      }

      try
      {
        if (_connection != null)
          await _connection.CloseAsync(cancellationToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error closing RabbitMQ channels");
      }
    }
  }
}