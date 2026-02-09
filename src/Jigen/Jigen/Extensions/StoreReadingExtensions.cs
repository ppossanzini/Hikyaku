using System.Collections.Concurrent;
using System.Data;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Jigen.DataStructures;

namespace Jigen.Extensions;

public static class StoreReadingExtensions
{
  internal static void ReadHeader(this Store store)
  {
    {
      var stream = store.EmbeddingFileStream;
      stream.Seek(0, SeekOrigin.End);
      store.VectorStoreHeader.TotalEntityCount = store.PositionIndex.Count == 0 ? 0 : store.PositionIndex.Max(i => i.Key);
      store.VectorStoreHeader.EmbeddingSize = store.Options.VectorSize;
      store.VectorStoreHeader.EmbeddingCurrentPosition = stream.Position;
    }

    {
      var stream = store.ContentFileStream;
      stream.Seek(0, SeekOrigin.End);
      store.VectorStoreHeader.ContentCurrentPosition = stream.Position;
    }
  }


  internal static void LoadIndex(this Store store)
  {
    var stream = store.IndexFileStream;
    if (stream.Length == 0) return;

    const int EntrySize = sizeof(long) * 4;

    stream.Seek(0, SeekOrigin.Begin);
    using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
    while (stream.Position + EntrySize <= stream.Length)
    {
      var id = reader.ReadInt64();
      var contentPosition = reader.ReadInt64();
      var embeddingsPosition = reader.ReadInt64();
      var size = reader.ReadInt64();

      store.PositionIndex[id] = (contentPosition, embeddingsPosition, size);
    }
  }


  public static string ReadContent(this Store store, long id)
  {
    var accessor = store.ContentData.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
    (long contentposition, long embeddingposition, long size) item;
    if (!store.PositionIndex.TryGetValue(id, out item)) return null;

    var contentid = accessor.ReadInt64(item.contentposition);
    if (contentid != id) throw new InvalidConstraintException("Content ID mismatch");

    byte[] buffer = new byte[item.size];
    accessor.ReadArray(item.contentposition + sizeof(long), buffer, 0, (int)item.size);
    return Encoding.UTF8.GetString(buffer).Trim();
  }

  private static VectorEmbeddings ReadNextVectorEmbedding(Store store, Stream stream, BinaryReader reader)
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