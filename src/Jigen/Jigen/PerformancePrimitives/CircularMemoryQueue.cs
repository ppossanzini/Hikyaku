using System.Runtime.InteropServices;

namespace Jigen.PerformancePrimitives;

[StructLayout(LayoutKind.Explicit, Size = 128)]
public struct ReadingWritingPositions()
{
  [FieldOffset(0)] public long WritingPosition = 0;
  [FieldOffset(64)] public long ReadingPosition = 0;
}

// [StructLayout(LayoutKind.Explicit, Size = 320)]
public class CircularMemoryQueue<T>(int bufferSize = 1000)
{
  private ReadingWritingPositions _positions = new();

  private readonly Memory<T> _buffer = new T[bufferSize];
  private readonly SemaphoreSlim _availableBufferPositions = new(bufferSize, bufferSize);


  public bool IsEmpty
  {
    get => Interlocked.Read(ref _positions.ReadingPosition) == Interlocked.Read(ref _positions.WritingPosition);
  }

  public bool IsFull
  {
    get => (Interlocked.Read(ref _positions.WritingPosition) % Interlocked.Read(ref _positions.ReadingPosition)) == bufferSize;
  }

  public long Count
  {
    get => Interlocked.Read(ref _positions.WritingPosition) % Interlocked.Read(ref _positions.ReadingPosition);
  }

  public async Task Enqueue(T item)
  {
    await _availableBufferPositions.WaitAsync();
    var position = (int)(Interlocked.Increment(ref _positions.WritingPosition) % bufferSize);
    _buffer.Span[position] = item;
  }

  public T Dequeue()
  {
    if (IsEmpty) return default!;

    var position = (int)(Interlocked.Increment(ref _positions.ReadingPosition) % bufferSize);
    var result = _buffer.Span[position];
    _buffer.Span[position] = default!;

    _availableBufferPositions.Release();
    return result;
  }

  public bool TryDequeue(out T result)
  {
    result = default;
    if (IsEmpty) return false;

    var position = (int)(Interlocked.Increment(ref _positions.ReadingPosition) % bufferSize);
    result = _buffer.Span[position];
    _buffer.Span[position] = default!;

    _availableBufferPositions.Release();
    return true;
  }

  public T Peek()
  {
    var position = (int)((Interlocked.Read(ref _positions.ReadingPosition) + 1) % bufferSize);
    return _buffer.Span[position];
  }
}