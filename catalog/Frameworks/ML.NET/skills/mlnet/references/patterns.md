# ML.NET Patterns

This reference covers essential ML.NET patterns for data loading, training, evaluation, and deployment.

## Data Loading Patterns

### Loading from CSV with Schema Definition

```csharp
public class HousingData(
    float Size,
    float Price,
    float Rooms,
    string Neighborhood)
{
    [LoadColumn(0)]
    public float Size { get; } = Size;

    [LoadColumn(1)]
    public float Price { get; } = Price;

    [LoadColumn(2)]
    public float Rooms { get; } = Rooms;

    [LoadColumn(3)]
    public string Neighborhood { get; } = Neighborhood;
}

public class DataLoaderService(MLContext mlContext)
{
    public IDataView LoadFromCsv(string path)
    {
        return mlContext.Data.LoadFromTextFile<HousingData>(
            path,
            hasHeader: true,
            separatorChar: ',');
    }

    public IDataView LoadFromCsvWithOptions(string path)
    {
        var options = new TextLoader.Options
        {
            HasHeader = true,
            Separators = [','],
            AllowQuoting = true,
            TrimWhitespace = true,
            MissingRealsAsNaNs = true
        };

        return mlContext.Data.LoadFromTextFile<HousingData>(path, options);
    }
}
```

### Loading from Database

```csharp
public class DatabaseLoaderService(MLContext mlContext)
{
    public IDataView LoadFromDatabase(string connectionString)
    {
        var loader = mlContext.Data.CreateDatabaseLoader<HousingData>();
        var source = new DatabaseSource(
            SqlClientFactory.Instance,
            connectionString,
            "SELECT Size, Price, Rooms, Neighborhood FROM HousingData");

        return loader.Load(source);
    }
}
```

### Loading from In-Memory Collections

```csharp
public class InMemoryLoaderService(MLContext mlContext)
{
    public IDataView LoadFromEnumerable(IEnumerable<HousingData> data)
    {
        return mlContext.Data.LoadFromEnumerable(data);
    }

    public IDataView LoadWithDefinedSchema(IEnumerable<HousingData> data)
    {
        var schema = SchemaDefinition.Create(typeof(HousingData));
        return mlContext.Data.LoadFromEnumerable(data, schema);
    }
}
```

### Streaming Large Datasets

```csharp
public class StreamingLoaderService(MLContext mlContext, int batchSize = 1000)
{
    public IEnumerable<IDataView> LoadInBatches(string path)
    {
        var allData = mlContext.Data.LoadFromTextFile<HousingData>(path, hasHeader: true);
        var batches = mlContext.Data.CreateEnumerable<HousingData>(allData, reuseRowObject: false);

        foreach (var batch in batches.Chunk(batchSize))
        {
            yield return mlContext.Data.LoadFromEnumerable(batch);
        }
    }
}
```

## Training Patterns

### Basic Training Pipeline

```csharp
public class RegressionTrainer(MLContext mlContext)
{
    public ITransformer Train(IDataView trainingData)
    {
        var pipeline = mlContext.Transforms.Categorical
            .OneHotEncoding("NeighborhoodEncoded", "Neighborhood")
            .Append(mlContext.Transforms.Concatenate(
                "Features",
                "Size",
                "Rooms",
                "NeighborhoodEncoded"))
            .Append(mlContext.Transforms.NormalizeMinMax("Features"))
            .Append(mlContext.Regression.Trainers.Sdca(
                labelColumnName: "Price",
                featureColumnName: "Features"));

        return pipeline.Fit(trainingData);
    }
}
```

### Cross-Validation Training

```csharp
public class CrossValidationTrainer(MLContext mlContext)
{
    public (ITransformer Model, double AverageRSquared) TrainWithCrossValidation(
        IDataView data,
        int numberOfFolds = 5)
    {
        var pipeline = mlContext.Transforms.Concatenate("Features", "Size", "Rooms")
            .Append(mlContext.Regression.Trainers.Sdca(
                labelColumnName: "Price",
                featureColumnName: "Features"));

        var cvResults = mlContext.Regression.CrossValidate(
            data,
            pipeline,
            numberOfFolds: numberOfFolds,
            labelColumnName: "Price");

        var averageRSquared = cvResults.Average(r => r.Metrics.RSquared);
        var bestModel = cvResults.OrderByDescending(r => r.Metrics.RSquared).First().Model;

        return (bestModel, averageRSquared);
    }
}
```

### Incremental Training

```csharp
public class IncrementalTrainer(MLContext mlContext, string modelPath)
{
    public ITransformer RetrainModel(IDataView newData)
    {
        ITransformer existingModel;
        DataViewSchema modelSchema;

        using (var stream = File.OpenRead(modelPath))
        {
            existingModel = mlContext.Model.Load(stream, out modelSchema);
        }

        var predictions = existingModel.Transform(newData);
        var retrainedModel = RefitModel(predictions);

        return retrainedModel;
    }

    private ITransformer RefitModel(IDataView data)
    {
        var pipeline = mlContext.Regression.Trainers.OnlineGradientDescent(
            labelColumnName: "Price",
            featureColumnName: "Features");

        return pipeline.Fit(data);
    }
}
```

### Multi-Class Classification Training

```csharp
public class ClassificationData(string Text, uint Label)
{
    public string Text { get; } = Text;
    public uint Label { get; } = Label;
}

public class MultiClassTrainer(MLContext mlContext)
{
    public ITransformer Train(IDataView data)
    {
        var pipeline = mlContext.Transforms.Text
            .FeaturizeText("Features", "Text")
            .Append(mlContext.Transforms.Conversion.MapValueToKey("Label"))
            .Append(mlContext.MulticlassClassification.Trainers
                .SdcaMaximumEntropy("Label", "Features"))
            .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

        return pipeline.Fit(data);
    }
}
```

## Evaluation Patterns

### Regression Evaluation

```csharp
public record RegressionEvaluationResult(
    double RSquared,
    double MeanAbsoluteError,
    double MeanSquaredError,
    double RootMeanSquaredError);

public class RegressionEvaluator(MLContext mlContext)
{
    public RegressionEvaluationResult Evaluate(ITransformer model, IDataView testData)
    {
        var predictions = model.Transform(testData);
        var metrics = mlContext.Regression.Evaluate(
            predictions,
            labelColumnName: "Price",
            scoreColumnName: "Score");

        return new RegressionEvaluationResult(
            metrics.RSquared,
            metrics.MeanAbsoluteError,
            metrics.MeanSquaredError,
            metrics.RootMeanSquaredError);
    }

    public void PrintMetrics(RegressionEvaluationResult result)
    {
        Console.WriteLine($"R-Squared: {result.RSquared:F4}");
        Console.WriteLine($"MAE: {result.MeanAbsoluteError:F4}");
        Console.WriteLine($"MSE: {result.MeanSquaredError:F4}");
        Console.WriteLine($"RMSE: {result.RootMeanSquaredError:F4}");
    }
}
```

### Binary Classification Evaluation

```csharp
public record BinaryClassificationResult(
    double Accuracy,
    double AreaUnderRocCurve,
    double F1Score,
    double PositivePrecision,
    double PositiveRecall);

public class BinaryClassificationEvaluator(MLContext mlContext)
{
    public BinaryClassificationResult Evaluate(ITransformer model, IDataView testData)
    {
        var predictions = model.Transform(testData);
        var metrics = mlContext.BinaryClassification.Evaluate(
            predictions,
            labelColumnName: "Label",
            scoreColumnName: "Score");

        return new BinaryClassificationResult(
            metrics.Accuracy,
            metrics.AreaUnderRocCurve,
            metrics.F1Score,
            metrics.PositivePrecision,
            metrics.PositiveRecall);
    }

    public CalibratedBinaryClassificationMetrics EvaluateWithConfusionMatrix(
        ITransformer model,
        IDataView testData)
    {
        var predictions = model.Transform(testData);
        return mlContext.BinaryClassification.Evaluate(predictions);
    }
}
```

### Multi-Class Evaluation

```csharp
public record MultiClassResult(
    double MacroAccuracy,
    double MicroAccuracy,
    double LogLoss,
    double LogLossReduction);

public class MultiClassEvaluator(MLContext mlContext)
{
    public MultiClassResult Evaluate(ITransformer model, IDataView testData)
    {
        var predictions = model.Transform(testData);
        var metrics = mlContext.MulticlassClassification.Evaluate(
            predictions,
            labelColumnName: "Label");

        return new MultiClassResult(
            metrics.MacroAccuracy,
            metrics.MicroAccuracy,
            metrics.LogLoss,
            metrics.LogLossReduction);
    }
}
```

### Feature Importance Analysis

```csharp
public class FeatureImportanceAnalyzer(MLContext mlContext)
{
    public ImmutableArray<RegressionMetricsStatistics> AnalyzePermutationFeatureImportance(
        ITransformer model,
        IDataView data)
    {
        var transformedData = model.Transform(data);

        var linearModel = model as ISingleFeaturePredictionTransformer<object>;
        if (linearModel is null)
        {
            throw new InvalidOperationException("Model does not support feature importance analysis");
        }

        var featureImportance = mlContext.Regression.PermutationFeatureImportance(
            linearModel,
            transformedData,
            labelColumnName: "Price",
            permutationCount: 50);

        return featureImportance;
    }
}
```

## Deployment Patterns

### Model Persistence

```csharp
public class ModelPersistenceService(MLContext mlContext)
{
    public void SaveModel(ITransformer model, DataViewSchema schema, string path)
    {
        using var stream = File.Create(path);
        mlContext.Model.Save(model, schema, stream);
    }

    public (ITransformer Model, DataViewSchema Schema) LoadModel(string path)
    {
        using var stream = File.OpenRead(path);
        var model = mlContext.Model.Load(stream, out var schema);
        return (model, schema);
    }

    public void SaveModelAsOnnx(
        ITransformer model,
        IDataView sampleData,
        string path)
    {
        using var stream = File.Create(path);
        mlContext.Model.ConvertToOnnx(model, sampleData, stream);
    }
}
```

### Prediction Engine Factory

```csharp
public class HousingPrediction
{
    [ColumnName("Score")]
    public float PredictedPrice { get; set; }
}

public class PredictionEngineFactory(MLContext mlContext, string modelPath)
{
    private readonly Lazy<PredictionEngine<HousingData, HousingPrediction>> _engine = new(() =>
    {
        using var stream = File.OpenRead(modelPath);
        var model = mlContext.Model.Load(stream, out _);
        return mlContext.Model.CreatePredictionEngine<HousingData, HousingPrediction>(model);
    });

    public HousingPrediction Predict(HousingData input)
    {
        return _engine.Value.Predict(input);
    }
}
```

### Thread-Safe Prediction Service

```csharp
public class ThreadSafePredictionService(MLContext mlContext, string modelPath) : IDisposable
{
    private readonly ObjectPool<PredictionEngine<HousingData, HousingPrediction>> _enginePool =
        CreatePool(mlContext, modelPath);

    private static ObjectPool<PredictionEngine<HousingData, HousingPrediction>> CreatePool(
        MLContext mlContext,
        string modelPath)
    {
        using var stream = File.OpenRead(modelPath);
        var model = mlContext.Model.Load(stream, out _);

        return mlContext.Model.CreatePredictionEnginePool<HousingData, HousingPrediction>(model);
    }

    public HousingPrediction Predict(HousingData input)
    {
        var engine = _enginePool.Get();
        try
        {
            return engine.Predict(input);
        }
        finally
        {
            _enginePool.Return(engine);
        }
    }

    public void Dispose()
    {
        // Pool handles disposal
    }
}
```

### ASP.NET Core Integration

```csharp
public static class MlNetServiceExtensions
{
    public static IServiceCollection AddMlNetPrediction<TInput, TOutput>(
        this IServiceCollection services,
        string modelPath)
        where TInput : class
        where TOutput : class, new()
    {
        services.AddSingleton<MLContext>();

        services.AddSingleton(sp =>
        {
            var mlContext = sp.GetRequiredService<MLContext>();
            using var stream = File.OpenRead(modelPath);
            return mlContext.Model.Load(stream, out _);
        });

        services.AddPredictionEnginePool<TInput, TOutput>();

        return services;
    }
}

// Usage in Program.cs
// builder.Services.AddMlNetPrediction<HousingData, HousingPrediction>("model.zip");

public class PredictionController(PredictionEnginePool<HousingData, HousingPrediction> predictionPool)
    : ControllerBase
{
    [HttpPost("predict")]
    public ActionResult<HousingPrediction> Predict([FromBody] HousingData input)
    {
        var prediction = predictionPool.Predict(input);
        return Ok(prediction);
    }
}
```

### Model Versioning and Hot-Reload

```csharp
public class VersionedModelService(
    MLContext mlContext,
    IOptionsMonitor<ModelOptions> options,
    ILogger<VersionedModelService> logger) : IDisposable
{
    private ITransformer? _currentModel;
    private readonly SemaphoreSlim _modelLock = new(1, 1);
    private FileSystemWatcher? _watcher;

    public async Task InitializeAsync()
    {
        await LoadModelAsync(options.CurrentValue.ModelPath);
        SetupFileWatcher(options.CurrentValue.ModelPath);
    }

    private async Task LoadModelAsync(string path)
    {
        await _modelLock.WaitAsync();
        try
        {
            using var stream = File.OpenRead(path);
            _currentModel = mlContext.Model.Load(stream, out _);
            logger.LogInformation("Model loaded from {Path}", path);
        }
        finally
        {
            _modelLock.Release();
        }
    }

    private void SetupFileWatcher(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? ".";
        var fileName = Path.GetFileName(path);

        _watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        _watcher.Changed += async (_, _) =>
        {
            await Task.Delay(500); // Debounce
            await LoadModelAsync(path);
        };

        _watcher.EnableRaisingEvents = true;
    }

    public async Task<TOutput> PredictAsync<TInput, TOutput>(TInput input)
        where TInput : class
        where TOutput : class, new()
    {
        await _modelLock.WaitAsync();
        try
        {
            var engine = mlContext.Model.CreatePredictionEngine<TInput, TOutput>(_currentModel!);
            return engine.Predict(input);
        }
        finally
        {
            _modelLock.Release();
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _modelLock.Dispose();
    }
}

public class ModelOptions
{
    public string ModelPath { get; set; } = "model.zip";
    public string Version { get; set; } = "1.0.0";
}
```

## Feature Engineering Patterns

### Text Featurization

```csharp
public class TextFeatureEngineer(MLContext mlContext)
{
    public IEstimator<ITransformer> CreateTextPipeline(string inputColumn, string outputColumn)
    {
        return mlContext.Transforms.Text.FeaturizeText(
            outputColumn,
            new TextFeaturizingEstimator.Options
            {
                WordFeatureExtractor = new WordBagEstimator.Options
                {
                    NgramLength = 2,
                    UseAllLengths = true
                },
                CharFeatureExtractor = new WordBagEstimator.Options
                {
                    NgramLength = 3,
                    UseAllLengths = false
                },
                Norm = TextFeaturizingEstimator.NormFunction.L2
            },
            inputColumn);
    }
}
```

### Categorical Encoding

```csharp
public class CategoricalEngineer(MLContext mlContext)
{
    public IEstimator<ITransformer> CreateOneHotPipeline(params string[] columns)
    {
        IEstimator<ITransformer>? pipeline = null;

        foreach (var column in columns)
        {
            var transform = mlContext.Transforms.Categorical.OneHotEncoding(
                $"{column}Encoded",
                column);

            pipeline = pipeline is null ? transform : pipeline.Append(transform);
        }

        return pipeline ?? mlContext.Transforms.PassThrough();
    }

    public IEstimator<ITransformer> CreateHashEncodingPipeline(string column, int numberOfBits = 16)
    {
        return mlContext.Transforms.Categorical.OneHotHashEncoding(
            $"{column}Hashed",
            column,
            numberOfBits: numberOfBits);
    }
}
```

### Missing Value Handling

```csharp
public class MissingValueHandler(MLContext mlContext)
{
    public IEstimator<ITransformer> CreateReplacementPipeline(
        string[] columns,
        MissingValueReplacingEstimator.ReplacementMode mode = MissingValueReplacingEstimator.ReplacementMode.Mean)
    {
        var inputOutputPairs = columns
            .Select(c => new InputOutputColumnPair(c, c))
            .ToArray();

        return mlContext.Transforms.ReplaceMissingValues(inputOutputPairs, mode);
    }

    public IEstimator<ITransformer> CreateIndicatorPipeline(string[] columns)
    {
        IEstimator<ITransformer>? pipeline = null;

        foreach (var column in columns)
        {
            var transform = mlContext.Transforms.IndicateMissingValues(
                $"{column}Missing",
                column);

            pipeline = pipeline is null ? transform : pipeline.Append(transform);
        }

        return pipeline ?? mlContext.Transforms.PassThrough();
    }
}
```

## Pipeline Composition Patterns

### Modular Pipeline Builder

```csharp
public class PipelineBuilder(MLContext mlContext)
{
    private readonly List<IEstimator<ITransformer>> _steps = [];

    public PipelineBuilder AddStep(IEstimator<ITransformer> step)
    {
        _steps.Add(step);
        return this;
    }

    public PipelineBuilder AddNormalization(params string[] columns)
    {
        foreach (var column in columns)
        {
            _steps.Add(mlContext.Transforms.NormalizeMinMax(column));
        }
        return this;
    }

    public PipelineBuilder AddFeatureConcatenation(string outputColumn, params string[] inputColumns)
    {
        _steps.Add(mlContext.Transforms.Concatenate(outputColumn, inputColumns));
        return this;
    }

    public IEstimator<ITransformer> Build()
    {
        if (_steps.Count == 0)
        {
            return mlContext.Transforms.PassThrough();
        }

        var pipeline = _steps[0];
        for (var i = 1; i < _steps.Count; i++)
        {
            pipeline = pipeline.Append(_steps[i]);
        }

        return pipeline;
    }
}
```
