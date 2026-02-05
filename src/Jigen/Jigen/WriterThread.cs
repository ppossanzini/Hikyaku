using Jigen.Extensions;

namespace Jigen;

public class Writer<T, TE>
  where T : struct where TE : struct
{
  private bool _running = true;

  // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
  private readonly Thread _writingThread;
  private readonly AutoResetEvent _waiter = new(false);

  private readonly Store<T, TE> _store;

  public Writer(Store<T, TE> store)
  {
    this._store = store;
    _writingThread = new Thread(WriterJob);
    _writingThread.IsBackground = false;
    _writingThread.Start();
  }

  internal void SignalNewData()
  {
    _waiter.Set();
  }

  async void WriterJob()
  {
    var spinwait = new SpinWait();
    while (_running)
    {
      if (_store.IngestionQueue.IsEmpty)
      {
        spinwait.Reset();
        while (spinwait.Count < 100)
          spinwait.SpinOnce();
      }

      _waiter.WaitOne(TimeSpan.FromSeconds(30));
      if (_store.IngestionQueue.IsEmpty) continue;

      while (_store.IngestionQueue.TryDequeue(out var entry))
      {
        _store.VerifyFileSize();
        _store.AppendContent(entry.Id, entry.Content, entry.Embedding);
      }

      await _store.SaveHeader();

      await Task.WhenAll([
        Task.Run(async () => await _store.ContentFileStream.FlushAsync()),
        Task.Run(async () => await _store.EmbeddingFileStream.FlushAsync()),
        Task.Run(async () => await _store.IndexFileStream.FlushAsync())
      ]);

      _store.EnableReading();
    }
  }

  public void Stop()
  {
    _running = false;
    _writingThread.Join();
  }
}