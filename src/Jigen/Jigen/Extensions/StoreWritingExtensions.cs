using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using Jigen.DataStructures;

namespace Jigen.Extensions;

public static class StoreWritingExtensions
{
  private static void WriteInt32Le(FileStream stream, int value)
  {
    Span<byte> buf = stackalloc byte[sizeof(int)];
    BinaryPrimitives.WriteInt32LittleEndian(buf, value);
    stream.Write(buf);
  }

  private static void WriteInt64Le(FileStream stream, long value)
  {
    Span<byte> buf = stackalloc byte[sizeof(long)];
    BinaryPrimitives.WriteInt64LittleEndian(buf, value);
    stream.Write(buf);
  }

  private static void WriteByteArray(FileStream stream, ReadOnlySpan<float> embeddings)
  {
    stream.Write(MemoryMarshal.AsBytes(embeddings));
  }


  private static async Task AppendIndex(
    this Store store,
    (long id, string collectioname, long contentposition, long embeddingposition, int dimensions, long contentsize) item)
  {
    if(!store.PositionIndex.ContainsKey(item.collectioname))
      store.PositionIndex[item.collectioname] = new Dictionary<long, (long, long, int, long)>();

    store.PositionIndex[item.collectioname][item.id] = (item.contentposition, item.embeddingposition, item.dimensions, item.contentsize);

    var file = store.IndexFileStream;

    file.Seek(0, SeekOrigin.End);
    WriteInt64Le(file, item.id);
    var nameAsBytes = Encoding.UTF8.GetBytes(item.collectioname);
    WriteInt32Le(file, nameAsBytes.Length);
    file.Write(nameAsBytes, 0, nameAsBytes.Length);
    WriteInt64Le(file, item.contentposition);
    WriteInt64Le(file, item.embeddingposition);
    WriteInt32Le(file, item.dimensions);
    WriteInt64Le(file, item.contentsize);

    await Task.CompletedTask;
  }


  public static async Task<VectorEntry> AppendContent<T>(this Store store, VectorEntry<T> entry)
  {
    entry.Id = Interlocked.Increment(ref store.VectorStoreHeader.TotalEntityCount);
    await store.IngestionQueue.EnqueueAsync(entry);
    store.Writer.SignalNewData();
    return entry;
  }

  // internal static async Task<(long id, long position, long embeddingPosition, long size)>
  //   AppendContent(this Store store, long id, string content, float[] embeddings)
  // {
  //   return await store.AppendContent(id, content, store.Options.QuantizationFunction(embeddings));
  // }

  internal static async Task<(long id, long position, long embeddingPosition, long size)>
    AppendContent(this Store store, long id, string collection, string content, float[] embeddings)
  {
    var contentStream = store.ContentFileStream;

    contentStream.Seek(0, SeekOrigin.End);
    var currentPosition = contentStream.Position;

    // Write content: id + utf8 bytes (size is tracked in index)
    WriteInt64Le(contentStream, id);
    var buffer = Encoding.UTF8.GetBytes(content);
    await contentStream.WriteAsync(buffer, 0, buffer.Length);
    long size = buffer.Length;

    store.VectorStoreHeader.ContentCurrentPosition = contentStream.Position;

    var embeddingsStream = store.EmbeddingFileStream;
    embeddingsStream.Seek(0, SeekOrigin.End);
    var embeddingPosition = embeddingsStream.Position;

    WriteInt64Le(embeddingsStream, id);
    WriteByteArray(embeddingsStream, embeddings);

    store.VectorStoreHeader.EmbeddingCurrentPosition = embeddingsStream.Position;

    await store.AppendIndex((id, collection, currentPosition, embeddingPosition, embeddings.Length, size));
    return (id, currentPosition, embeddingPosition, size);
  }
}