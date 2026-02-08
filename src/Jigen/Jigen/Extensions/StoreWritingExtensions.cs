using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using Jigen.DataStructures;

namespace Jigen.Extensions;

public static class StoreWritingExtensions
{
  // internal static async Task SaveHeader<T, TE>(this Store<T, TE> store)
  //   where T : struct where TE : struct
  // {
  //   {
  //     var file = store.EmbeddingFileStream;
  //     await using var accessor = new BinaryWriter(file, Encoding.UTF8, true);
  //
  //     file.Seek(0, SeekOrigin.Begin);
  //
  //     accessor.Write(store.VectorStoreHeader.TotalEntityCount);
  //     accessor.Write(store.VectorStoreHeader.EmbeddingSize);
  //     accessor.Write(store.VectorStoreHeader.EmbeddingCurrentPosition);
  //     accessor.Flush();
  //     await file.FlushAsync();
  //     file.Seek(0, SeekOrigin.End);
  //     
  //     
  //   }
  //
  //   {
  //     var file = store.ContentFileStream;
  //     await using var accessor = new BinaryWriter(file, Encoding.UTF8, true);
  //
  //     file.Seek(0, SeekOrigin.Begin);
  //     accessor.Write(store.VectorStoreHeader.ContentCurrentPosition);
  //     accessor.Flush();
  //     await file.FlushAsync();
  //     file.Seek(0, SeekOrigin.End);
  //     
  //   }
  // }


  private static void WriteInt32LE(FileStream stream, int value)
  {
    Span<byte> buf = stackalloc byte[sizeof(int)];
    BinaryPrimitives.WriteInt32LittleEndian(buf, value);
    stream.Write(buf);
  }

  private static void WriteInt64LE(FileStream stream, long value)
  {
    Span<byte> buf = stackalloc byte[sizeof(long)];
    BinaryPrimitives.WriteInt64LittleEndian(buf, value);
    stream.Write(buf);
  }

  private static void WriteByteArray<TE>(FileStream stream, ReadOnlySpan<TE> embeddings)
    where TE : struct
  {
    stream.Write(MemoryMarshal.AsBytes(embeddings));
  }

  internal static async Task RewriteIndex<T, TE>(this Store<T, TE> store)
    where T : struct where TE : struct
  {
    // Rewrite in append-only format (v2): no count, just fixed-size entries.
    var stream = store.IndexFileStream;

    stream.Seek(0, SeekOrigin.Begin);
    stream.SetLength(0);

    foreach (var kv in store.PositionIndex)
    {
      WriteInt64LE(stream, kv.Key);
      WriteInt64LE(stream, kv.Value.contentposition);
      WriteInt64LE(stream, kv.Value.embeddingsposition);
      WriteInt64LE(stream, kv.Value.size);
    }

    await stream.FlushAsync();
  }

  private static async Task AppendIndex<T, TE>(
    this Store<T, TE> store,
    (long id, long contentposition, long embeddingposition, long contentsize) item)
    where T : struct where TE : struct
  {
    store.PositionIndex[item.id] = (item.contentposition, item.embeddingposition, item.contentsize);

    var file = store.IndexFileStream;

    file.Seek(0, SeekOrigin.End);
    WriteInt64LE(file, item.id);
    WriteInt64LE(file, item.contentposition);
    WriteInt64LE(file, item.embeddingposition);
    WriteInt64LE(file, item.contentsize);

    await Task.CompletedTask;
  }


  public static async Task<VectorEntry<T>> AppendContent<T, TE>(this Store<T, TE> store, VectorEntry<T> entry)
    where T : struct where TE : struct
  {
    entry.Id = Interlocked.Increment(ref store.VectorStoreHeader.TotalEntityCount);
    await store.IngestionQueue.Enqueue(entry);
    store.Writer.SignalNewData();
    return entry;
  }

  internal static async Task<(long id, long position, long embeddingPosition, long size)>
    AppendContent<T, TE>(this Store<T, TE> store, long id, string content, T[] embeddings)
    where T : struct where TE : struct
  {
    return await store.AppendContent(id, content, store.Options.QuantizationFunction(embeddings));
  }

  private static async Task<(long id, long position, long embeddingPosition, long size)>
    AppendContent<T, TE>(this Store<T, TE> store, long id, string content, TE[] embeddings)
    where T : struct where TE : struct
  {
    var contentStream = store.ContentFileStream;

    contentStream.Seek(0, SeekOrigin.End);
    var currentPosition = contentStream.Position;

    // Write content: id + utf8 bytes (size is tracked in index)
    WriteInt64LE(contentStream, id);
    var buffer = Encoding.UTF8.GetBytes(content);
    await contentStream.WriteAsync(buffer, 0, buffer.Length);
    long size = buffer.Length;

    store.VectorStoreHeader.ContentCurrentPosition = contentStream.Position;

    var embeddingsStream = store.EmbeddingFileStream;
    embeddingsStream.Seek(0, SeekOrigin.End);
    var embeddingPosition = embeddingsStream.Position;

    WriteInt64LE(embeddingsStream, id);
    WriteInt64LE(embeddingsStream, currentPosition);

    WriteByteArray<TE>(embeddingsStream, embeddings);

    store.VectorStoreHeader.EmbeddingCurrentPosition = embeddingsStream.Position;

    await store.AppendIndex((id, currentPosition, embeddingPosition, size));
    return (id, currentPosition, embeddingPosition, size);
  }
}