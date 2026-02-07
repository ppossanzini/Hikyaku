using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;
using Xunit.Abstractions;
using ZstdSharp.Unsafe;

namespace JigenTests;

public class ReadWriteTest : IDisposable
{
  private readonly ITestOutputHelper _testOutputHelper;
  private Store<float, float> _store;
  private Jigen.SemanticTools.OnnxEmbeddingGenerator _embeddingGenerator;

  public ReadWriteTest(ITestOutputHelper testOutputHelper)
  {
    _testOutputHelper = testOutputHelper;
    _store = new Store<float, float>(new StoreOptions<float, float>()
    {
      DataBaseName = "readwritetest",
      DataBasePath = "/data/jigendb",
      QuantizationFunction = i => i,//.Normalize().Quantize().ToArray(), 
      VectorSize = 1024, InitialVectorDBSize = 1, InitialContentDBSize = 1
    });

    _embeddingGenerator = new(
      "/data/onnx/multi-lingual/tokenizer.onnx",
      "/data/onnx/multi-lingual/model.onnx");
  }

  public void Dispose()
  {
    _store.Dispose();
  }


  [Fact]
  public async Task Write()
  {
    string[] sentences =
    [
      "L'intelligenza artificiale sta trasformando il settore tecnologico.",
      "Il machine learning è una branca fondamentale dell'informatica moderna.",
      "La ricetta della pasta alla carbonara prevede guanciale e uova.",
      "Cucinare gli spaghetti con il guanciale è una tradizione romana.",
      "Il cambiamento climatico rappresenta una minaccia per la biodiversità.",
      "Le emissioni di CO2 contribuiscono al riscaldamento globale.",
      "Il cane corre felice nel parco inseguendo una pallina.",
      "Gli animali domestici richiedono cure e attenzione costante.",
      "La Juventus ha vinto la partita di campionato ieri sera.",
      "Il mondo del calcio è scosso da nuove notizie di mercato.",
      "Sviluppare software richiede logica e molta pazienza.",
      "La programmazione funzionale è un paradigma molto potente."
    ];

    foreach (var s in sentences)
    {
      var rr = await _store.AppendContent(new VectorEntry<float>()
      {
        Content = s, Embedding = _embeddingGenerator.GenerateEmbedding(s), Id = 0
      });

      _testOutputHelper.WriteLine($"{rr.Id} - {rr.Content} - {String.Concat(rr.Embedding.Take(10).Select(i => $"{i},"))}");
    }

    await _store.SaveChangesAsync();
    await _store.Close();
  }

  [Fact]
  public async Task Read()
  {
    for (int i = 0; i < 12; i++)
    {
      _testOutputHelper.WriteLine(_store.ReadContent(i + 1));
    }

    await _store.Close();
  }

  [Theory]
  [InlineData("animali pelosi")]
  [InlineData("trofei conquistati")]
  [InlineData("viaggiare in lazio")]
  public async Task Search(string search)
  {
    var query = _embeddingGenerator.GenerateEmbedding(search);
    _testOutputHelper.WriteLine($"{String.Concat(query.Take(10).Select(i => $"{i},"))}");

    var results = _store.Search(query, 5);

    _testOutputHelper.WriteLine($"Hai cercato: {search}");
    foreach (var r in results)
    {
      _testOutputHelper.WriteLine($"{r.entry.Id} {r.entry.Content} {r.score}");
    }

    await _store.Close();
  }
}