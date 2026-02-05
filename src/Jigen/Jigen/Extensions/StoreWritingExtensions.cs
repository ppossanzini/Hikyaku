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
  internal static async Task SaveHeader<T,TE>(this Store<T,TE> store)
  where T:struct where TE:struct
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

  internal static async Task RewriteIndex<T,TE>(this Store<T,TE> store)
    where T:struct where TE:struct
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

  private static void AppendIndex<T,TE>(this Store<T,TE> store, (long id, long contentposition, long embeddingposition, long contentsize) item)
    where T:struct where TE:struct
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

  public static async Task<VectorEntry<T>> AppendContent<T,TE>(this Store<T,TE> store, VectorEntry<T> entry)
    where T:struct where TE:struct
  {
    entry.Id = Interlocked.Increment(ref store.VectorStoreHeader.TotalEntityCount);
    await store.IngestionQueue.Enqueue(entry);
    store.Writer.SignalNewData();
    return entry;
  }
  
  internal static (long id, long position, long embeddingPosition, long size) 
    AppendContent<T,TE>(this Store<T,TE> store, long id, string content, T[] embeddings)
    where T:struct where TE:struct
  {
    return store.AppendContent(id, content, store.Options.QuantizationFunction(embeddings));
  }
  
  private static (long id, long position, long embeddingPosition, long size) AppendContent<T,TE>(this Store<T,TE> store, long id, string content, ReadOnlySpan<TE> embeddings)
    where T:struct where TE:struct
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