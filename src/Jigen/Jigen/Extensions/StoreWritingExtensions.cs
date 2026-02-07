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

  internal static async Task RewriteIndex<T, TE>(this Store<T, TE> store)
    where T : struct where TE : struct
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

  private static async Task AppendIndex<T, TE>(this Store<T, TE> store, (long id, long contentposition, long embeddingposition, long contentsize) item)
    where T : struct where TE : struct
  {
    store.PositionIndex.Add(item.id, (item.contentposition, item.embeddingposition, item.contentsize));
    var file = store.IndexFileStream;

    if (file.Length == 0)
      await file.WriteAsync(BitConverter.GetBytes(0), 0, sizeof(int));

    file.Seek(0, SeekOrigin.End);
    await file.WriteAsync(BitConverter.GetBytes(item.id), 0, sizeof(long));
    await file.WriteAsync(BitConverter.GetBytes(item.contentposition), 0, sizeof(long));
    await file.WriteAsync(BitConverter.GetBytes(item.embeddingposition), 0, sizeof(long));
    await file.WriteAsync(BitConverter.GetBytes(item.contentsize), 0, sizeof(long));
    file.Seek(0, SeekOrigin.End);


    file.Seek(0, SeekOrigin.Begin);
    await file.WriteAsync(BitConverter.GetBytes(store.PositionIndex.Count), 0, sizeof(int));
    await file.FlushAsync();
    file.Seek(0, SeekOrigin.End);
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
    ;


    await contentStream.WriteAsync(BitConverter.GetBytes(id), 0, sizeof(long));
    var buffer = Encoding.UTF8.GetBytes(content);
    await contentStream.WriteAsync(buffer, 0, buffer.Length);
    long size = buffer.Length;

    contentStream.Seek(0, SeekOrigin.End);
    store.VectorStoreHeader.ContentCurrentPosition = contentStream.Position;
    // Vector Normalization


    var embeddingsStream = store.EmbeddingFileStream;
    embeddingsStream.Seek(0, SeekOrigin.End);
    var embeddingPosition = embeddingsStream.Position;

    {
      await embeddingsStream.WriteAsync(BitConverter.GetBytes(id), 0, sizeof(long));
      await embeddingsStream.WriteAsync(BitConverter.GetBytes(currentPosition), 0, sizeof(long));
      await embeddingsStream.WriteAsync(MemoryMarshal.AsBytes((Span<TE>)embeddings).ToArray());
    }
    embeddingsStream.Seek(0, SeekOrigin.End);
    store.VectorStoreHeader.EmbeddingCurrentPosition = embeddingsStream.Position;

    await store.AppendIndex((id, currentPosition, embeddingPosition, size));


    return (id, currentPosition, embeddingPosition, size);
  }
}