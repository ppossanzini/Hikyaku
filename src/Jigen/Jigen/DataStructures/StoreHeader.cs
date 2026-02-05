namespace Jigen.DataStructures;

public class StoreHeader
{
  public int EmbeddingSize;
  public long TotalEntityCount;

  public long EmbeddingCurrentPosition { get; set; }
  public long ContentCurrentPosition { get; set; }
}