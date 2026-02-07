using System.Collections.Concurrent;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using Jigen.DataStructures;

// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable SuggestVarOrType_BuiltInTypes

namespace Jigen.Extensions;

public static class SimdSearchExtensions
{
  public static unsafe List<(VectorEntry<TEmbeddings> entry, float score)> Search<TEmbeddings, TEmbeddingVector>(this Store<TEmbeddings, TEmbeddingVector> store, float[] queryVector, int top)
    where TEmbeddings : struct
    where TEmbeddingVector : struct
  {
    if (queryVector is null) throw new ArgumentNullException(nameof(queryVector));
    if (top <= 0) return [];

    var vectorSize = store.Options.VectorSize;
    var topResults = new ConcurrentBag<(long Id, float Score)>();


    using (var accessor = store.EmbeddingsData.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read))
    {
      try
      {
        byte* pointer = null;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);

        var headerOffset = 2 * sizeof(long) + sizeof(int);
        var entrySize = sizeof(long) + sizeof(long) + (vectorSize * sizeof(TEmbeddingVector));
        var totalBytes = (long)store.VectorStoreHeader.EmbeddingCurrentPosition;
        var entryCount = (totalBytes - headerOffset) / entrySize;

        Parallel.For(0L, entryCount, i =>
        {
          var offset = headerOffset + (i * entrySize);
          var currentPtr = pointer + offset;

          long id = *(long*)currentPtr;
          float* vectorBase = (float*)(currentPtr + (sizeof(long) * 2));

          fixed (float* pQuery = queryVector) // Fissiamo la query in memoria
          {
            // Passiamo i puntatori al metodo SIMD
            float similarity = DotProductSimdUnsafe(pQuery, vectorBase, vectorSize);
            topResults.Add((id, similarity));
          }
        });
      }
      finally
      {
        accessor.SafeMemoryMappedViewHandle.ReleasePointer();
      }
    }

    return topResults.OrderByDescending(r => r.Score).Take(top)
      .Select(r => (new VectorEntry<TEmbeddings>
      {
        Id = r.Id, Content = store.ReadContent(r.Id)
      }, r.Score))
      .ToList();
  }

  private static unsafe float DotProductSimdUnsafe(float* leftPtr, float* rightPtr, int length)
  {
    int simdWidth = Vector<float>.Count;
    Vector<float> sumVector = Vector<float>.Zero;
    int i = 0;

    for (; i <= length - simdWidth; i += simdWidth)
    {
      // Caricamento diretto da puntatore a registro SIMD
      var v1 = *(Vector<float>*)(leftPtr + i);
      var v2 = *(Vector<float>*)(rightPtr + i);
      sumVector += v1 * v2;
    }

    // Somma orizzontale dei componenti del vettore
    float result = Vector.Sum(sumVector);

    // Gestione dei rimanenti elementi (tail cleanup)
    for (; i < length; i++) result += leftPtr[i] * rightPtr[i];

    return result;
  }
}