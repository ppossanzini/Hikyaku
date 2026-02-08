using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Hikyaku.Kaido.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Type = System.Type;

namespace Hikyaku.Kaido.GRPC
{
  public class RequestsManager : GrpcServices.GrpcServicesBase
  {
    private readonly ILogger<RequestsManager> _logger;
    private readonly IServiceProvider _provider;
    private readonly MessageDispatcherOptions _options;

    private readonly HashSet<string> _deDuplicationCache = new HashSet<string>();
    private readonly SHA256 _hasher = SHA256.Create();

    private readonly Dictionary<string, Type> _typeMappings;

    public RequestsManager(IOptions<MessageDispatcherOptions> options, ILogger<RequestsManager> logger, IServiceProvider provider,
      IOptions<RouterOptions> routerOptions, IOptions<RequestsManagerOptions> requestsManagerOptions)
    {
      if (requestsManagerOptions.Value.AcceptMessageTypes.Count == 0)
      {
        foreach (var t in routerOptions.Value.LocalTypes)
          requestsManagerOptions.Value.AcceptMessageTypes.Add(t);
      }

      this._options = options.Value;
      this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this._provider = provider;

      _typeMappings = requestsManagerOptions.Value.AcceptMessageTypes.ToDictionary(k => k.AxonTypeName(routerOptions.Value), v => v);
    }


    public override async Task<MessageResponse> ManageHikyakuMessage(RequestMessage request, ServerCallContext context)
    {
      _logger.LogDebug("Message Received. Looking for type");
      if (_typeMappings.TryGetValue(request.HikyakuType, out var messageType))
      {
        var consumerMethod = typeof(RequestsManager)
          .GetMethod(nameof(RequestsManager.ManageGenericHikyakuMessage), BindingFlags.Instance | BindingFlags.NonPublic)?
          .MakeGenericMethod(messageType);

        var response = await ((Task<string>)consumerMethod!.Invoke(this, new object[] { request }))!;
        return new MessageResponse() { Body = response };
      }

      return null;
    }

    public override async Task<Empty> ManageHikyakuNotification(NotifyMessage request, ServerCallContext context)
    {
      _logger.LogDebug("Notification Received. Looking for type");
      if (_typeMappings.TryGetValue(request.HikyakuType, out var messageType))
      {
        var consumerMethod = typeof(RequestsManager)
          .GetMethod(nameof(RequestsManager.ManageGenericHikyakuNotification), BindingFlags.Instance | BindingFlags.NonPublic)?
          .MakeGenericMethod(messageType);

        await ((Task)consumerMethod!.Invoke(this, new object[] { request }))!;
      }

      return new Empty();
    }

    private async Task<string> ManageGenericHikyakuMessage<T>(RequestMessage request)
    {
      string responseMsg = null;
      try
      {
        var msg = request.Body;
        _logger.LogDebug("Elaborating message : {0}", msg);
        var message = JsonConvert.DeserializeObject<T>(msg, _options.SerializerSettings);


        var orchestrator = _provider.CreateScope().ServiceProvider.GetRequiredService<IHikyaku>();
        var response = await orchestrator.SendObject(message);
        responseMsg = JsonConvert.SerializeObject(new ResponseMessage { Content = response, Status = StatusEnum.Ok },
          _options.SerializerSettings);
        _logger.LogDebug("Elaborating sending response : {0}", responseMsg);
      }
      catch (Exception ex)
      {
        responseMsg = JsonConvert.SerializeObject(new ResponseMessage
          {
            Exception = ex,
            OriginaStackTrace = ex.StackTrace,
            Status = StatusEnum.Exception, Content = MediatR.Unit.Value
          },
          _options.SerializerSettings);
        _logger.LogError(ex, $"Error executing message of type {typeof(T)} from external service");
      }

      return responseMsg;
    }

    private async Task ManageGenericHikyakuNotification<T>(NotifyMessage request)
    {
      var kaido = _provider.CreateScope().ServiceProvider.GetRequiredService<IHikyaku>();
      var router = kaido as Kaido;
      try
      {
        var msg = request.Body;

        if (_options.DeDuplicationEnabled)
        {
          var hash = msg.GetHash(_hasher);
          lock (_deDuplicationCache)
            if (_deDuplicationCache.Contains(hash))
            {
              _logger.LogDebug($"duplicated message received : {request.HikyakuType}");
              return;
            }

          lock (_deDuplicationCache)
            _deDuplicationCache.Add(hash);

          // Do not await this task
#pragma warning disable CS4014
          Task.Run(async () =>
          {
            await Task.Delay(_options.DeDuplicationTTL);
            lock (_deDuplicationCache)
              _deDuplicationCache.Remove(hash);
          });
#pragma warning restore CS4014
        }

        _logger.LogDebug("Elaborating notification : {0}", msg);
        var message = JsonConvert.DeserializeObject<T>(msg, _options.SerializerSettings);

        router?.StopPropagating();
        await kaido.PublishObject(message);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, $"Error executing message of type {typeof(T)} from external service");
      }
      finally
      {
        router?.ResetPropagating();
      }
    }
  }
}