using Jigen.Extensions;

namespace Jigen;

public class Writer<T, TE>
  where T : struct where TE : struct
{
  private bool _running = true;

  // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
  private readonly Thread _writingThread;
  private readonly AutoResetEvent _waiter = new(false);
  private readonly ManualResetEvent _writingcompleted = new(false);

  private readonly Store<T, TE> _store;

  public Task WaitForWritingCompleted => Task.Run(() => _writingcompleted.WaitOne());

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

      _writingcompleted.Set();
      _waiter.WaitOne(TimeSpan.FromSeconds(2));
      if (_store.IngestionQueue.IsEmpty) continue;
      _writingcompleted.Reset();
      
      while (_store.IngestionQueue.TryDequeue(out var entry))
      {
        // _store.VerifyFileSize();
        Console.WriteLine($"Writing entry with ID: {entry.Id}");
        var result = await _store.AppendContent(entry.Id, entry.Content, entry.Embedding);
        Console.WriteLine($"Writing completed at position: {result.id} {result.position}");
        
      }

      // await _store.SaveHeader();

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