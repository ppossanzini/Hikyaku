namespace Jigen.DataStructures;

public class VectorEntry<TEmbedding> where TEmbedding : struct
{
  public long Id { get; set; }
  public string Content { get; set; }
  public TEmbedding[] Embedding { get; set; }
}