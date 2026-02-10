using System.Collections;
using Jigen.DataStructures;
using Jigen.Extensions;

namespace Jigen;

public class VectorCollection<T> : ICollection<T>
  where T : VectorEntry<T>, new()
{
  private readonly Store _store;
  private readonly int _dimensions;
  private readonly string _name;

  public VectorCollection(Store store, int dimensions = 1536, string name = nameof(VectorCollection<T>))
  {
    if (string.IsNullOrEmpty(name)) throw new ArgumentException("Collection name cannot be null or empty", nameof(name));
    if (name.Length > 256) throw new ArgumentException("Collection name cannot be longer than 256 characters", nameof(name));

    this._store = store ?? throw new ArgumentNullException(nameof(store));
    this._dimensions = dimensions;
    this._name = name;

    store.PositionIndex.Add(name, new Dictionary<byte[], (long contentposition, long embeddingsposition, int dimensions, long size)>(ByteArrayEqualityComparer.Instance));
  }

  public IEnumerator<T> GetEnumerator()
  {
    throw new NotImplementedException();
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }

  public void Add(T item)
  {
    if (item == null) return;
    if (_dimensions != item.Embedding.Length) throw new InvalidOperationException($"Dimensions mismatch: expected {_dimensions}, got {item.Embedding.Length}");

    item.CollectionName = _name;
    _store.AppendContent(item).GetAwaiter().GetResult();
  }

  public void Clear()
  {
    if (_store.PositionIndex.TryGetValue(_name, out var positionIndex))
      positionIndex.Clear();
  }

  public bool Contains(T item)
  {
    if (item == null) return false;

    if (_store.PositionIndex.TryGetValue(_name, out var positionIndex))
      return positionIndex.ContainsKey(item.Id);
    return false;
  }

  public void CopyTo(T[] array, int arrayIndex)
  {
    throw new NotImplementedException();
  }

  public bool Remove(T item)
  {
    if (item == null) return false;
    return _store.PositionIndex.TryGetValue(_name, out var positionIndex) && positionIndex.Remove(item.Id);
  }

  public int Count => _store.PositionIndex.TryGetValue(_name, out var index) ? index.Count : 0;
  public bool IsReadOnly { get; } = false;
}