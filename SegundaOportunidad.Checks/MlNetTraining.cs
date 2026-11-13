using System.Text.Json;
using Microsoft.ML;
using SegundaOportunidad.Services;

internal static class MlNetTraining
{
    public static void Run()
    {
        var ml = new MLContext(seed: 2026);
        var output = Path.GetFullPath("SegundaOportunidad/App_Data");
        using var examples = JsonDocument.Parse(File.ReadAllText("ml/classification-examples.json"));
        var training = Read(examples.RootElement.GetProperty("training"));
        var validation = Read(examples.RootElement.GetProperty("validation"));
        var train = ml.Data.LoadFromEnumerable(training);
        var test = ml.Data.LoadFromEnumerable(validation);
        var pipeline = ml.Transforms.Text.FeaturizeText("Features", nameof(MlNetTextInput.Text))
            .Append(ml.BinaryClassification.Trainers.SdcaLogisticRegression(
                labelColumnName: nameof(MlNetTextInput.Label), featureColumnName: "Features"));
        var classifier = pipeline.Fit(train);
        var metrics = ml.BinaryClassification.Evaluate(classifier.Transform(test));
        ml.Model.Save(classifier, train.Schema, Path.Combine(output, "mlnet-classifier.zip"));

        using var corpus = JsonDocument.Parse(File.ReadAllText("ml/demo-corpus.json"));
        var documents = corpus.RootElement.EnumerateArray().Where(row => !row.TryGetProperty("available", out var active) || active.GetBoolean())
            .Select(row => new MlNetTextInput { Text = string.Join(' ',
                row.GetProperty("title").GetString(), row.GetProperty("title").GetString(), row.GetProperty("title").GetString(),
                row.GetProperty("category").GetString(), row.GetProperty("category").GetString(),
                row.GetProperty("description").GetString(), row.GetProperty("mode").GetString(), row.GetProperty("condition").GetString()) }).ToList();
        var corpusData = ml.Data.LoadFromEnumerable(documents);
        var recommender = ml.Transforms.Text.FeaturizeText("Features", nameof(MlNetTextInput.Text)).Fit(corpusData);
        ml.Model.Save(recommender, corpusData.Schema, Path.Combine(output, "mlnet-content.zip"));
        var report = new { framework = "Microsoft.ML 5.0.1", trainingExamples = training.Count,
            validationExamples = validation.Count, documents = documents.Count,
            accuracy = metrics.Accuracy, auc = metrics.AreaUnderRocCurve, f1 = metrics.F1Score,
            scope = "Datos sintéticos. Validación separada por ejemplos; no demuestra detección de fraude en usuarios reales." };
        File.WriteAllText(Path.Combine(output, "mlnet-evaluation.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(report));
    }

    private static List<MlNetTextInput> Read(JsonElement group) =>
        group.GetProperty("regular").EnumerateArray().Select(row => new MlNetTextInput { Text = row.GetString()!, Label = false })
            .Concat(group.GetProperty("review").EnumerateArray().Select(row => new MlNetTextInput { Text = row.GetString()!, Label = true })).ToList();
}
