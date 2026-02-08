namespace Hikyaku;

/// <summary>
/// Interface to customize routing queue names using message parameters. 
/// </summary>
public interface IRouteTo
{
  /// <summary>
  /// a function to customize queue name
  /// </summary>
  /// <param name="baseroute">Basic queue name from hikyaku</param>
  /// <returns>Customized queue name</returns>
  string RouteTo(string baseroute);
}