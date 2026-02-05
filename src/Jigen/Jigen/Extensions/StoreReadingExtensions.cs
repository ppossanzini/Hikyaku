using System.Collections.Concurrent;
using System.Data;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Jigen.DataStructures;

namespace Jigen.Extensions;

public static class StoreReadingExtensions
{
  internal static void ReadHeader<T, TE>(this Store<T, TE> store)
    where T : struct where TE : struct
  {
    {
      var stream = store.EmbeddingFileStream;

      var br = new BinaryReader(stream, Encoding.UTF8, true);

      if (stream.Length < sizeof(long) + sizeof(int)) return;
      stream.Position = 0;
      store.VectorStoreHeader.TotalEntityCount = br.ReadInt64();
      store.VectorStoreHeader.EmbeddingSize = br.ReadInt32();
      store.VectorStoreHeader.EmbeddingCurrentPosition = br.ReadInt64();

      stream.Position = store.VectorStoreHeader.EmbeddingCurrentPosition;
    }

    {
      var stream = store.ContentFileStream;
      var br = new BinaryReader(stream, Encoding.UTF8, true);
      if (stream.Length < sizeof(long) + sizeof(int)) return;
      stream.Position = 0;
      store.VectorStoreHeader.ContentCurrentPosition = br.ReadInt64();
      stream.Position = store.VectorStoreHeader.ContentCurrentPosition;
    }
  }

  internal static void LoadIndex<T,TE>(this Store<T,TE> store)
    where T : struct where TE : struct
  {
    var stream = store.IndexFileStream;
    if (stream.Length == 0) return;

    using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

    var count = reader.ReadInt32();
    for (var i = 0; i < count; i++)
    {
      var id = reader.ReadInt64();
      var contentPosition = reader.ReadInt64();
      var embeddingsPosition = reader.ReadInt64();
      var size = reader.ReadInt64();

      store.PositionIndex.Add(id, (contentPosition, embeddingsPosition, size));
    }
  }


  public static string ReadContent<T,TE>(this Store<T,TE> store, long id)
    where T : struct where TE : struct
  {
    var accessor = store.ContentData.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
    (long contentposition, long embeddingposition, long size) item;
    if (!store.PositionIndex.TryGetValue(id, out item)) return null;

    var contentid = accessor.ReadInt64(item.contentposition);
    if (contentid != id) throw new InvalidConstraintException("Content ID mismatch");

    byte[] buffer = new byte[item.size];
    accessor.ReadArray(item.contentposition + sizeof(long), buffer, 0, (int)item.size);
    return Encoding.UTF8.GetString(buffer);
  }

  private static VectorEmbeddings ReadNextVectorEmbedding<T,TE>(Store<T,TE> store, Stream stream, BinaryReader reader)
    where T : struct where TE : struct
  {
    var embeddings = new float[store.Options.VectorSize];
    var bytes = MemoryMarshal.AsBytes(embeddings.AsSpan());

    var id = reader.ReadInt64();
    var position = reader.ReadInt64();

    stream.ReadExactly(bytes);

    return new VectorEmbeddings
    {
      Id = id,
      Position = position,
      Embeddings = embeddings
    };
  }
}