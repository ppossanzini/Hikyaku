using Jigen;
using Jigen.Extensions;
using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel;

namespace JigenTests;

public class SearchTests
{
  private Store<float, sbyte> _store;

  public SearchTests()
  {
    _store = new Store<float, sbyte>(new StoreOptions<float, sbyte>()
    {
      DataBaseName = "openai_llm_forsearch",
      DataBasePath = "/data/jigendb",
      QuantizationFunction = i => i.Normalize().Quantize().ToArray()
    });
  }

  [Fact]
  public void Ingestion()
  {
    
    var kernel = Kernel.CreateBuilder()
      .AddOnnxRuntimeGenAIChatCompletion("phi-3", modelPath)
      .AddBertOnnxEmbeddingGenerator(
        onnxModelPath: @"C:\ai-models\bge-micro-v2\onnx\model.onnx",
        vocabPath: @"C:\ai-models\bge-micro-v2\vocab.txt")
      .Build();
    
    
    
// Carica il tokenizer usando il file tokenizer.json scaricato dal repo
    var tokenizerData = File.ReadAllText("tokenizer.json");
    var tokenizer = new Microsoft.ML.Tokenizers.SentencePieceTokenizer(modelProto:)

// Esempio di codifica
    var result = tokenizer.Encode("Ciao, come va?");
    var ids = result.Ids;
  }
}