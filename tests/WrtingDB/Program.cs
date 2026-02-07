// See https://aka.ms/new-console-template for more information

using Jigen;
using Jigen.DataStructures;
using Jigen.Extensions;

Console.WriteLine("Hello, World!");

var store = new Store<float, float>(new StoreOptions<float, float>()
{
  DataBaseName = "readwritetest",
  DataBasePath = "/data/jigendb",
  QuantizationFunction = i => i, //.Normalize().Quantize().ToArray(), 
  VectorSize = 1024, InitialVectorDBSize = 1, InitialContentDBSize = 1
});

var embeddingGenerator = new Jigen.SemanticTools.OnnxEmbeddingGenerator(
  "/data/onnx/multi-lingual/tokenizer.onnx",
  "/data/onnx/multi-lingual/model.onnx");
  
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
  var rr = await store.AppendContent(new VectorEntry<float>()
  {
    Content = s, Embedding = embeddingGenerator.GenerateEmbedding(s), Id = 0
  });

  Console.WriteLine($"{rr.Id} - {rr.Content} - {String.Concat(rr.Embedding.Take(10).Select(i => $"{i},"))}");
}

await store.SaveChangesAsync();
await store.Close();