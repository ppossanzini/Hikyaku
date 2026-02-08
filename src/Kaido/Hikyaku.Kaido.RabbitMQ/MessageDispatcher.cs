using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Newtonsoft.Json;
using System.Threading;
using System.Text;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Hikyaku.Kaido.Messages;
using Microsoft.Extensions.DependencyInjection;


namespace Hikyaku.Kaido.RabbitMQ
{
  /// <summary>
  /// Class for dispatching messages to RabbitMQ and handling responses.
  /// </summary>
  public class MessageDispatcher : IExternalMessageDispatcher, IDisposable
  {
    /// <summary>
    /// Represents the options for the message dispatcher.
    /// </summary> 
    private readonly MessageDispatcherOptions _options;

    /// <summary>
    /// The logger for the MessageDispatcher class.
    /// </summary>
    private readonly ILogger<MessageDispatcher> _logger;

    private readonly RouterOptions _routerOptions;

    /// <summary>
    /// Stores an instance of an object that implements the IConnection interface.
    /// </summary>
    private IConnection _connection;

    /// <summary>
    /// The channel used for sending messages.
    /// </summary>
    private IChannel _sendChannel;

    /// <summary>
    /// Represents the name of the reply queue.
    /// </summary>
    private string _replyQueueName;

    /// <summary>
    /// Represents an asynchronous event-based consumer for sending messages.
    /// </summary>
    private AsyncEventingBasicConsumer _sendConsumer;

    /// <summary>
    /// The unique identifier of the consumer.
    /// </summary>
    private string _consumerId;

    /// <summary>
    /// Dictionary that maps callback strings to TaskCompletionSource objects.
    /// </summary>
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _callbackMapper =
      new ConcurrentDictionary<string, TaskCompletionSource<string>>();


    /// <summary>
    /// Constructor for the MessageDispatcher class. </summary> <param name="options">The options for the MessageDispatcher.</param> <param name="logger">The logger for the MessageDispatcher.</param>
    /// <param name="routerOptions"></param>
    /// /
    public MessageDispatcher(
      IOptions<MessageDispatcherOptions> options,
      ILogger<MessageDispatcher> logger, IOptions<RouterOptions> routerOptions)
    {
      this._options = options.Value;
      this._logger = logger;
      this._routerOptions = routerOptions.Value;

      this.InitConnection().Wait();
    }

    /// Initializes the RabbitMQ connection and sets up the necessary channels and consumers.
    /// /
    private async Task InitConnection()
    {
      // Ensuring we have a connection object
      if (_connection == null)
      {
        _logger.LogInformation($"Creating RabbitMQ Connection to '{_options.HostName}'...");
        var factory = new ConnectionFactory
        {
          HostName = _options.HostName,
          UserName = _options.UserName,
          Password = _options.Password,
          VirtualHost = _options.VirtualHost,
          Port = _options.Port,
          ClientProvidedName = _options.ClientName,
          MaxInboundMessageBodySize = _options.MaxMessageSize
        };

        factory.AutomaticRecoveryEnabled = true;
        factory.NetworkRecoveryInterval = TimeSpan.FromSeconds(10);
        factory.TopologyRecoveryEnabled = true;

        _connection = await factory.CreateConnectionAsync();
      }

      _sendChannel = await _connection.CreateChannelAsync();
      await _sendChannel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Topic);
      // _channel.ConfirmSelect();

      var queueName = $"{_options.QueueName}.{Process.GetCurrentProcess().Id}.{DateTime.Now.Ticks}";
      _replyQueueName = (await _sendChannel.QueueDeclareAsync(queue: queueName)).QueueName;
      _sendConsumer = new AsyncEventingBasicConsumer(_sendChannel);
      _sendConsumer.ReceivedAsync += (_, ea) =>
      {
        TaskCompletionSource<string> tcs = null;
        try
        {
          if (!_callbackMapper.TryRemove(ea.BasicProperties.CorrelationId ?? "", out tcs))
            return Task.CompletedTask;


          var body = ea.Body.ToArray();
          var response = Encoding.UTF8.GetString(body);
          tcs.TrySetResult(response);
        }
        catch (Exception ex)
        {
          _logger.LogError($"Error deserializing response: {ex.Message}", ex);
          tcs?.TrySetException(ex);
        }

        return Task.CompletedTask;
      };

      _sendChannel.BasicReturnAsync += (_, ea) =>
      {
        if (!_callbackMapper.TryRemove(ea.BasicProperties.CorrelationId ?? "", out var tcs)) return Task.CompletedTask;
        tcs.TrySetException(new Exception($"Unable to deliver required action: {ea.RoutingKey}"));
        return Task.CompletedTask;
      };

      await _sendChannel.BasicQosAsync(0, Math.Max(_options.PerConsumerQos, (ushort)1), false);
      this._consumerId = await _sendChannel.BasicConsumeAsync(queue: _replyQueueName, autoAck: true, consumer: _sendConsumer);
    }


    public bool CanDispatch<TRequest>()
    {
      if (_options.DispatchOnly.Count > 0)
        return _options.DispatchOnly.Contains(typeof(TRequest));

      if (_options.DontDispatch.Count > 0)
        return !_options.DontDispatch.Contains(typeof(TRequest));

      return true;
    }

    /// <summary>
    /// Dispatches a request and waits for the response.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="request">The request object.</param>
    /// <param name="queueName">Dispatcher Queue name</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task representing the response message.</returns>
    public async Task<ResponseMessage<TResponse>> Dispatch<TRequest, TResponse>(TRequest request,
      CancellationToken cancellationToken = default)
    {
      var internalQueue = typeof(TRequest).AxonTypeName(_routerOptions);
      string queueName = (request as IRouteTo)?.RouteTo(internalQueue);

      var message = JsonConvert.SerializeObject(request, _options.SerializerSettings);

      var correlationId = Guid.NewGuid().ToString();

      var tcs = new TaskCompletionSource<string>();
      var _ = _callbackMapper.TryAdd(correlationId, tcs);

      await _sendChannel.BasicPublishAsync(
        exchange: _options.ExchangeName,
        routingKey: queueName ?? internalQueue,
        mandatory: true,
        body: Encoding.UTF8.GetBytes(message),
        basicProperties: GetBasicProperties(correlationId), cancellationToken: cancellationToken);

      cancellationToken.Register(() => _callbackMapper.TryRemove(correlationId, out var _));
      var result = await tcs.Task;

      return JsonConvert.DeserializeObject<ResponseMessage<TResponse>>(result, _options.SerializerSettings);
    }

    /// <summary>
    /// Sends a notification message to the specified exchange and routing key.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request message.</typeparam>
    /// <param name="request">The request message to send.</param>
    /// <param name="queueName">The dispatcher queue name</param>
    /// <param name="cancellationToken">A cancellation token to cancel the notification operation.</param>
    /// <returns>A task representing the asynchronous notification operation.</returns>
    public async Task Notify<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : INotification
    {
      var internalQueue = request.GetType().AxonTypeName(_routerOptions);
      string queueName = (request as IRouteTo)?.RouteTo(internalQueue);

      var message = JsonConvert.SerializeObject(request, _options.SerializerSettings);

      _logger.LogInformation($"Sending message to: {_options.ExchangeName}/{queueName ?? internalQueue}");

      await _sendChannel.BasicPublishAsync(
        exchange: _options.ExchangeName,
        routingKey: queueName ?? internalQueue,
        mandatory: false,
        body: Encoding.UTF8.GetBytes(message)
      );
      
      _logger.LogInformation($"Sending message to: {_options.ExchangeName}/{queueName ?? internalQueue} .. completed");
    }


    /// <summary>
    /// Retrieves the basic properties for a given correlation ID.
    /// </summary>
    /// <param name="correlationId">The correlation ID associated with the properties.</param>
    /// <returns>The basic properties object.</returns>
    private BasicProperties GetBasicProperties(string correlationId)
    {
      var props = new BasicProperties();
      props.CorrelationId = correlationId;
      props.ReplyTo = _replyQueueName;
      return props;
    }

    /// <summary>
    /// Disposes of the resources used by the object.
    /// </summary>
    public void Dispose()
    {
      try
      {
        this._logger.LogInformation("Closing Connection...");
        _sendChannel?.BasicCancelAsync(_consumerId).Wait();
        _sendChannel?.CloseAsync().Wait();
        // _connection.Close();
      }
      catch (Exception)
      {
        // ignored
      }
    }
  }
}