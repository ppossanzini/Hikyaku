using Jigen.Extensions;

namespace Jigen;

public class Writer<T, TE>
  where T : struct where TE : struct
{
  private volatile bool _running = true;

  private readonly Thread _writingThread;
  private readonly Thread _flusher;

  private readonly AutoResetEvent _waiter = new(false);
  private readonly ManualResetEvent _writingCompleted = new(true); // queue drained at start

  // Wake flusher on Stop() so it doesn't wait 30s
  private readonly AutoResetEvent _flushWake = new(false);

  // Single lock guarding ALL stream I/O (writes + flush)
  private readonly object _ioLock = new();

  private readonly Store<T, TE> _store;

  // Policy (3): "completed" == queue drained (not flushed)
  public Task WaitForWritingCompleted => Task.Run(() => _writingCompleted.WaitOne());

  public Writer(Store<T, TE> store)
  {
    _store = store;
    _writingThread = new Thread(WriterJob) { IsBackground = true };
    _writingThread.Start();

    _flusher = new Thread(FlushJob) { IsBackground = true };
    _flusher.Start();
  }

  internal void SignalNewData()
  {
    _writingCompleted.Reset(); // there is (or will be) work to do
    _waiter.Set();
  }


  private void FlushJob()
  {
    while (_running)
    {
      // Wait 30s or until Stop() wakes us
      _flushWake.WaitOne(TimeSpan.FromSeconds(30));
      if (!_running) break;

      // Flush only when writer is idle (queue drained)
      if (!_writingCompleted.WaitOne(0)) continue;

      lock (_ioLock)
      {
        // FlushAsync + GetResult is OK here since we're on a dedicated thread
        _store.EmbeddingFileStream.FlushAsync().GetAwaiter().GetResult();
        _store.ContentFileStream.FlushAsync().GetAwaiter().GetResult();
        _store.IndexFileStream.FlushAsync().GetAwaiter().GetResult();
      }
    }
  }

  private void WriterJob()
  {
    while (_running)
    {
      _waiter.WaitOne(TimeSpan.FromMilliseconds(200));
      if (!_running) break;

      if (_store.IngestionQueue.IsEmpty)
      {
        _writingCompleted.Set();
        continue;
      }

      try
      {
        lock (_ioLock)
        {
          while (_store.IngestionQueue.TryDequeue(out var entry))
          {
            _store.AppendContent(entry.Id, entry.Content, entry.Embedding).GetAwaiter().GetResult();
          }
        }
      }
      finally
      {
        if (_store.IngestionQueue.IsEmpty)
          _writingCompleted.Set();
      }
    }

    _writingCompleted.Set();
  }

  public void Stop()
  {
    _running = false;

    // Wake both threads promptly
    _waiter.Set();
    _flushWake.Set();

    _writingThread.Join();
    _flusher.Join();
  }
}