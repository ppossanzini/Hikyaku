using System.Data.SqlTypes;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Jigen.DataStructures;

// ReSharper disable MemberCanBePrivate.Global

namespace Jigen.Extensions;

public static class StoreWritingExtensions
{
  internal static async Task SaveHeader(this Store store)
  {
    {
      var file = store.EmbeddingFileStream;
      await using var accessor = new BinaryWriter(file, Encoding.UTF8, true);

      file.Position = 0;
      accessor.Write(store.VectorStoreHeader.TotalEntityCount);
      accessor.Write(store.VectorStoreHeader.EmbeddingSize);
      accessor.Write(store.VectorStoreHeader.EmbeddingCurrentPosition);
      accessor.Flush();
      await file.FlushAsync();
    }

    {
      var file = store.ContentFileStream;
      await using var accessor = new BinaryWriter(file, Encoding.UTF8, true);

      file.Position = 0;
      accessor.Write(store.VectorStoreHeader.ContentCurrentPosition);
      await file.FlushAsync();
    }
  }

  internal static async Task RewriteIndex(this Store store)
  {
    var stream = store.IndexFileStream;
    await using var sw = new BinaryWriter(stream, Encoding.UTF8, true);

    stream.Seek(0, SeekOrigin.Begin);
    sw.Write(store.PositionIndex.Count);

    foreach (var kv in store.PositionIndex)
    {
      sw.Write(kv.Key);
      sw.Write(kv.Value.contentposition);
      sw.Write(kv.Value.embeddingsposition);
    }

    sw.Flush();
    await stream.FlushAsync();
  }

  private static void AppendIndex(this Store store, (long id, long contentposition, long embeddingposition, long contentsize) item)
  {
    store.PositionIndex.Add(item.id, (item.contentposition, item.embeddingposition, item.contentsize));
    var file = store.IndexFileStream;
    using var sw = new BinaryWriter(file, Encoding.UTF8, true);

    if (file.Length == 0)
      sw.Write((int)0);

    file.Seek(0, SeekOrigin.End);
    sw.Write(item.id);
    sw.Write(item.contentposition);
    sw.Write(item.embeddingposition);
    sw.Write(item.contentsize);

    file.Seek(0, SeekOrigin.Begin);
    sw.Write(store.PositionIndex.Count);

    sw.Flush();
  }

  public static async Task<VectorEntry> AppendContent(this Store store, VectorEntry entry)
  {
    entry.Id = Interlocked.Increment(ref store.VectorStoreHeader.TotalEntityCount);
    await store.IngestionQueue.Enqueue(entry);
    store.Writer.SignalNewData();
    return entry;
  }
  
  internal static (long id, long position, long embeddingPosition, long size) AppendContent(this Store store, long id, string content, ReadOnlySpan<float> embeddings)
  {
    return store.AppendContent(id, content, embeddings.Normalize().Quantize());
  }
  
  private static (long id, long position, long embeddingPosition, long size) AppendContent(this Store store, long id, string content, ReadOnlySpan<sbyte> embeddings)
  {
    var contentStream = store.ContentFileStream;
    using var contentSw = new BinaryWriter(contentStream, Encoding.UTF8, true);

    var currentPosition = 0L;
    var size = 0L;

    currentPosition = contentStream.Position;

    contentSw.Write(id);
    var buffer = Encoding.UTF8.GetBytes(content);
    contentStream.Write(buffer, 0, buffer.Length);

    contentSw.Flush();
    size = contentStream.Position - currentPosition;

    store.VectorStoreHeader.ContentCurrentPosition = contentStream.Position;
    // Vector Normalization


    var embeddingsStream = store.EmbeddingFileStream;
    using var embeddingSw = new BinaryWriter(embeddingsStream, Encoding.UTF8, true);
    var embeddingPosition = embeddingsStream.Position;

    {
      embeddingSw.Write(id);
      embeddingSw.Write(currentPosition);
      embeddingsStream.Write(MemoryMarshal.AsBytes(embeddings));
      embeddingSw.Flush();
    }
    store.VectorStoreHeader.EmbeddingCurrentPosition = embeddingsStream.Position;

    store.AppendIndex((id, currentPosition, embeddingPosition, size));


    return (id, currentPosition, embeddingPosition, size);
  }
}