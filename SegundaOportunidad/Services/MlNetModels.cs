using Microsoft.ML;
using Microsoft.ML.Data;

namespace SegundaOportunidad.Services;

public sealed class MlNetTextInput
{
    public string Text { get; set; } = "";
    public bool Label { get; set; }
}

public sealed class MlNetClassificationOutput
{
    [ColumnName("PredictedLabel")]
    public bool NeedsReview { get; set; }
    public float Probability { get; set; }
}

public sealed class MlNetVector
{
    public float[] Features { get; set; } = [];
}

public sealed record MlNetClassification(string Categoria, float ProbabilidadRevision, string Modelo, string Alcance);

public sealed class MlNetModels
{
    private readonly MLContext context = new(seed: 2026);
    private readonly Lazy<ITransformer> classifier;
    private readonly Lazy<ITransformer> recommender;

    public MlNetModels(IWebHostEnvironment environment)
    {
        var directory = Path.Combine(environment.ContentRootPath, "App_Data");
        classifier = new(() => context.Model.Load(Path.Combine(directory, "mlnet-classifier.zip"), out _));
        recommender = new(() => context.Model.Load(Path.Combine(directory, "mlnet-content.zip"), out _));
    }

    public MlNetClassification Classify(string text)
    {
        using var engine = context.Model.CreatePredictionEngine<MlNetTextInput, MlNetClassificationOutput>(classifier.Value);
        var result = engine.Predict(new MlNetTextInput { Text = text });
        return new(result.NeedsReview ? "Revisión sugerida" : "Contenido habitual",
            result.Probability, "ML.NET · clasificación binaria SDCA",
            "Entrenado con ejemplos sintéticos. Orientación para revisión humana; no determina fraude ni aplica sanciones.");
    }

    public IReadOnlyList<float[]> Vectorize(IEnumerable<string> texts)
    {
        var data = context.Data.LoadFromEnumerable(texts.Select(text => new MlNetTextInput { Text = text }));
        return context.Data.CreateEnumerable<MlNetVector>(recommender.Value.Transform(data), reuseRowObject: false)
            .Select(row => row.Features).ToList();
    }

    public static double Similarity(float[] first, float[] second)
    {
        double dot = 0, firstNorm = 0, secondNorm = 0;
        for (var index = 0; index < first.Length; index++)
        {
            dot += first[index] * second[index];
            firstNorm += first[index] * first[index];
            secondNorm += second[index] * second[index];
        }
        return firstNorm == 0 || secondNorm == 0 ? 0 : dot / Math.Sqrt(firstNorm * secondNorm);
    }
}
