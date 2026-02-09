namespace Jigen.DataStructures;

public class VectorEntry
{
  public long Id { get; set; }
  public string CollectionName { get; set; }
  public string Content { get; set; }
  public float[] Embedding { get; set; }
}

public class VectorEntry<T> : VectorEntry
{
  // public T Content { get; set; }
}