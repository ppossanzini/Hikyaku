using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using Confluent.Kafka;
using System.Threading.Tasks;
using System;
using System.Threading;
using Hikyaku.Kaido.Messages;
using Microsoft.Extensions.DependencyInjection;


namespace Hikyaku.Kaido.Kafka
{
  /// <summary>
  /// Represents a MessageDispatcher class that is responsible for dispatching and handling messages using Kafka.
  /// </summary>
  public class MessageDispatcher : IExternalMessageDispatcher, IDisposable
  {
    private Thread _consumerThread;
    private readonly MessageDispatcherOptions _options;
    private readonly ILogger<MessageDispatcher> _logger;
    private readonly IServiceProvider _provider;
    private readonly RouterOptions _hikyakuOptions;
    private IProducer<Null, string> _producer;
    private IConsumer<Null, string> _consumer;
    private string _replyTopicName;

    private bool _running = true;

    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _callbackMapper =
      new ConcurrentDictionary<string, TaskCompletionSource<string>>();

    public MessageDispatcher(IOptions<MessageDispatcherOptions> options, ILogger<MessageDispatcher> logger, IServiceProvider provider,
      IOptions<RouterOptions> hikyakuOptions)
    {
      this._options = options.Value;
      this._logger = logger;
      _provider = provider;
      _hikyakuOptions = hikyakuOptions.Value;
      this.InitConnection();
    }


    /// Initializes the Kafka connection.
    /// This method sets up a Kafka connection by creating a producer and consumer, subscribing to a reply topic, and starting a consumer thread to listen for responses.
    /// @remarks
    /// It is assumed that the `_options` and `_logger` objects are properly configured.
    /// @example
    /// InitConnection();
    /// @see KafkaOptions
    /// @see ILogger
    /// @see ProducerBuilder
    /// @see ConsumerBuilder
    /// @see IAdminClient
    /// @see JsonConvert
    /// /
    public void InitConnection()
    {
      _logger.LogInformation($"Creating Kafka Connection to '{_options.BootstrapServers}'...");

      // Ensuring we have a connection object

      _replyTopicName = $"{Process.GetCurrentProcess().Id}.{DateTime.Now.Ticks}";
      var config = this._options.GetConsumerConfig();
      config.GroupId = _replyTopicName;

      _producer = new ProducerBuilder<Null, string>(this._options.GetProducerConfig()).Build();
      _consumer = new ConsumerBuilder<Null, string>(config).Build();

      _provider.CreateTopicAsync(_options, _replyTopicName);

      _consumer.Subscribe(_replyTopicName);
      _consumerThread = new Thread(() =>
        {
          while (_running)
          {
            var consumeResult = _consumer.Consume();
            if (consumeResult != null)
            {
              _logger.LogDebug("Response Message: {Msg}", consumeResult.Message.Value);
              var reply = JsonConvert.DeserializeObject<KafkaReply>(consumeResult.Message.Value, this._options.SerializerSettings);

              if (reply != null)
                if (_callbackMapper.TryRemove(reply.CorrelationId, out var tcs))
                  tcs?.TrySetResult(consumeResult.Message.Value);
            }
          }
        }
      );
      _consumerThread.IsBackground = true;
      _consumerThread.Start();
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
    /// Dispatches a request message to a Kafka topic and waits for a response.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request message.</typeparam>
    /// <typeparam name="TResponse">The type of the response message.</typeparam>
    /// <param name="request">The request message to be dispatched.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the dispatch operation.</param>
    /// <returns>A task that represents the asynchronous dispatch operation. The task result contains the response message.</returns>
    public async Task<ResponseMessage<TResponse>> Dispatch<TRequest, TResponse>(TRequest request,
      CancellationToken cancellationToken = default)
    {
      var internalQueue = typeof(TRequest).AxonTypeName(_hikyakuOptions);
      string queueName = (request as IRouteTo)?.RouteTo(internalQueue);

      var correlationId = Guid.NewGuid().ToString();
      var message = JsonConvert.SerializeObject(new KafkaMessage<TRequest>
      {
        Message = request,
        CorrelationId = correlationId,
        ReplyTo = _replyTopicName
      }, _options.SerializerSettings);


      var tcs = new TaskCompletionSource<string>();
      _callbackMapper.TryAdd(correlationId, tcs);

      var internalTypeName = typeof(TRequest).AxonTypeName(_hikyakuOptions);
      var routeto = request as IRouteTo;

      await _producer.ProduceAsync(
        topic: queueName ?? internalQueue,
        message: new Message<Null, string> { Value = message }, cancellationToken);

      cancellationToken.Register(() => _callbackMapper.TryRemove(correlationId, out var _));
      var result = await tcs.Task;

      var response = JsonConvert.DeserializeObject<KafkaReply<ResponseMessage<TResponse>>>(result, this._options.SerializerSettings);
      return response.Reply;
    }

    /// <summary>
    /// Notifies the specified request.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <param name="request">The request object.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public async Task Notify<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : INotification
    {
      var internalQueue = typeof(TRequest).AxonTypeName(_hikyakuOptions);
      string queueName = (request as IRouteTo)?.RouteTo(internalQueue);

      var message = JsonConvert.SerializeObject(request, _options.SerializerSettings);

      _logger.LogInformation($"Sending message to: {Consts.HikyakuExchangeName}/{queueName ?? internalQueue}");

      await _producer.ProduceAsync(
        topic: queueName ?? internalQueue,
        message: new Message<Null, string> { Value = message }, cancellationToken);
    }

    public void Dispose()
    {
      try
      {
        _running = false;
      }
      finally
      {
        try
        {
          _producer.Dispose();
        }
        catch
        {
        }

        try
        {
          DisposeConsumer();
        }
        catch
        {
        }
      }
    }

    /// <summary>
    /// Disposes the consumer by deleting the topic asynchronously, unsubscribing, closing, and disposing the consumer.
    /// </summary>
    private void DisposeConsumer()
    {
      _provider.DeleteTopicAsync(this._options, this._replyTopicName);
      _consumer.Unsubscribe();
      _consumer.Close();
      _consumer.Dispose();
    }
  }
}