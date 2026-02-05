using Jigen;

namespace JigenTests;

public class SearchTests
{
  private Store _store;

  public SearchTests()
  {
    _store = new Store(new StoreOptions()
    {
      DataBaseName = "openai_llm_forsearch",
      DataBasePath = "/data/jigendb"
    });
  }
  
  
  
}