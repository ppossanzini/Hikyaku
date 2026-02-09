using System.Collections.Concurrent;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using Jigen.DataStructures;

// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable SuggestVarOrType_BuiltInTypes

namespace Jigen.Extensions;

public static class SimdSearchExtensions
{
  public static unsafe List<(VectorEntry entry, float score)> Search(this Store store, string collection, float[] queryVector, int top)
  {
    if (queryVector is null) throw new ArgumentNullException(nameof(queryVector));
    if (top <= 0) return [];

    var topResults = new ConcurrentBag<(long Id, float Score)>();


    using (var accessor = store.EmbeddingsData.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read))
    {
      try
      {
        byte* pointer = null;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);


        if (store.PositionIndex.TryGetValue(collection, out var index) == false) return [];

        Parallel.ForEach(index.Values, i =>
        {
          var offset = i.embeddingsposition;
          var currentPtr = pointer + offset;

          long id = *(long*)currentPtr;
          float* vectorBase = (float*)(currentPtr + sizeof(long));

          fixed (float* pQuery = queryVector) // Fissiamo la query in memoria
          {
            // Passiamo i puntatori al metodo SIMD
            float similarity = DotProductSimdUnsafe(pQuery, vectorBase, i.dimensions);
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
      .Select(r => (new VectorEntry
      {
        Id = r.Id, Content = store.ReadContent(collection, r.Id)
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