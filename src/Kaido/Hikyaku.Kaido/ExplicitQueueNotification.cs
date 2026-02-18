
using System;

namespace Hikyaku.Kaido
{
  [Obsolete("Use IRouteTo interface instead", true)]
  public class ExplicitQueueNotification<T> : IExplicitQueue, INotification
    where T : INotification
  {
    public T Message { get; set; }
    public string QueueName { get; set; }

    object IExplicitQueue.MessageObject
    {
      get => Message;
    }
  }

  interface IExplicitQueue
  {
    string QueueName { get; }
    object MessageObject { get; }
  }
}