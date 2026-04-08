# ML.NET Examples

This reference provides complete examples for common machine learning scenarios using ML.NET.

## Sentiment Analysis

Binary classification for detecting positive or negative sentiment in text.

```csharp
public class SentimentData(string Text, bool Sentiment)
{
    [LoadColumn(0)]
    public string Text { get; } = Text;

    [LoadColumn(1), ColumnName("Label")]
    public bool Sentiment { get; } = Sentiment;
}

public class SentimentPrediction
{
    [ColumnName("PredictedLabel")]
    public bool Prediction { get; set; }

    public float Probability { get; set; }

    public float Score { get; set; }
}

public class SentimentAnalysisService(MLContext mlContext)
{
    public ITransformer TrainModel(string dataPath)
    {
        var data = mlContext.Data.LoadFromTextFile<SentimentData>(
            dataPath,
            hasHeader: true,
            separatorChar: '\t');

        var split = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

        var pipeline = mlContext.Transforms.Text
            .FeaturizeText("Features", nameof(SentimentData.Text))
            .Append(mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(
                labelColumnName: "Label",
                featureColumnName: "Features"));

        var model = pipeline.Fit(split.TrainSet);

        // Evaluate
        var predictions = model.Transform(split.TestSet);
        var metrics = mlContext.BinaryClassification.Evaluate(predictions, "Label");

        Console.WriteLine($"Accuracy: {metrics.Accuracy:P2}");
        Console.WriteLine($"AUC: {metrics.AreaUnderRocCurve:P2}");
        Console.WriteLine($"F1 Score: {metrics.F1Score:P2}");

        return model;
    }

    public SentimentPrediction Predict(ITransformer model, string text)
    {
        var engine = mlContext.Model.CreatePredictionEngine<SentimentData, SentimentPrediction>(model);
        return engine.Predict(new SentimentData(text, false));
    }
}

// Usage
public class SentimentAnalysisExample
{
    public void Run()
    {
        var mlContext = new MLContext(seed: 42);
        var service = new SentimentAnalysisService(mlContext);

        var model = service.TrainModel("sentiment_data.tsv");

        var result = service.Predict(model, "This product is amazing!");
        Console.WriteLine($"Sentiment: {(result.Prediction ? "Positive" : "Negative")}");
        Console.WriteLine($"Probability: {result.Probability:P2}");
    }
}
```

## Price Prediction (Regression)

Predicting house prices based on features.

```csharp
public class HouseData(
    float Size,
    float Bedrooms,
    float Bathrooms,
    float Age,
    string Location,
    float Price)
{
    [LoadColumn(0)]
    public float Size { get; } = Size;

    [LoadColumn(1)]
    public float Bedrooms { get; } = Bedrooms;

    [LoadColumn(2)]
    public float Bathrooms { get; } = Bathrooms;

    [LoadColumn(3)]
    public float Age { get; } = Age;

    [LoadColumn(4)]
    public string Location { get; } = Location;

    [LoadColumn(5)]
    public float Price { get; } = Price;
}

public class HousePricePrediction
{
    [ColumnName("Score")]
    public float PredictedPrice { get; set; }
}

public class HousePricePredictionService(MLContext mlContext)
{
    public ITransformer TrainModel(IDataView data)
    {
        var split = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

        var pipeline = mlContext.Transforms.Categorical
            .OneHotEncoding("LocationEncoded", nameof(HouseData.Location))
            .Append(mlContext.Transforms.Concatenate(
                "Features",
                nameof(HouseData.Size),
                nameof(HouseData.Bedrooms),
                nameof(HouseData.Bathrooms),
                nameof(HouseData.Age),
                "LocationEncoded"))
            .Append(mlContext.Transforms.NormalizeMinMax("Features"))
            .Append(mlContext.Regression.Trainers.FastTree(
                labelColumnName: nameof(HouseData.Price),
                featureColumnName: "Features",
                numberOfLeaves: 20,
                numberOfTrees: 100,
                minimumExampleCountPerLeaf: 10,
                learningRate: 0.2));

        var model = pipeline.Fit(split.TrainSet);

        // Evaluate
        var predictions = model.Transform(split.TestSet);
        var metrics = mlContext.Regression.Evaluate(
            predictions,
            labelColumnName: nameof(HouseData.Price));

        Console.WriteLine($"R-Squared: {metrics.RSquared:F4}");
        Console.WriteLine($"RMSE: {metrics.RootMeanSquaredError:F2}");
        Console.WriteLine($"MAE: {metrics.MeanAbsoluteError:F2}");

        return model;
    }

    public float PredictPrice(ITransformer model, HouseData house)
    {
        var engine = mlContext.Model.CreatePredictionEngine<HouseData, HousePricePrediction>(model);
        return engine.Predict(house).PredictedPrice;
    }
}
```

## Image Classification

Classifying images using transfer learning with pre-trained models.

```csharp
public class ImageData(string ImagePath, string Label)
{
    public string ImagePath { get; } = ImagePath;
    public string Label { get; } = Label;
}

public class ImagePrediction
{
    [ColumnName("Score")]
    public float[] Score { get; set; } = [];

    public string PredictedLabel { get; set; } = string.Empty;
}

public class ImageClassificationService(MLContext mlContext)
{
    public ITransformer TrainModel(string imagesFolder)
    {
        var images = LoadImagesFromDirectory(imagesFolder);
        var data = mlContext.Data.LoadFromEnumerable(images);
        var split = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

        var pipeline = mlContext.Transforms.Conversion
            .MapValueToKey("LabelKey", nameof(ImageData.Label))
            .Append(mlContext.Transforms.LoadRawImageBytes(
                "Image",
                imagesFolder,
                nameof(ImageData.ImagePath)))
            .Append(mlContext.MulticlassClassification.Trainers.ImageClassification(
                featureColumnName: "Image",
                labelColumnName: "LabelKey",
                arch: ImageClassificationTrainer.Architecture.ResnetV2101,
                epoch: 100,
                batchSize: 10,
                learningRate: 0.01f,
                validationSet: split.TestSet))
            .Append(mlContext.Transforms.Conversion.MapKeyToValue(
                "PredictedLabel",
                "PredictedLabel"));

        return pipeline.Fit(split.TrainSet);
    }

    private static IEnumerable<ImageData> LoadImagesFromDirectory(string folder)
    {
        var extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp" };

        return Directory.GetDirectories(folder)
            .SelectMany(labelFolder =>
            {
                var label = Path.GetFileName(labelFolder);
                return Directory.GetFiles(labelFolder)
                    .Where(file => extensions.Contains(Path.GetExtension(file).ToLower()))
                    .Select(file => new ImageData(file, label));
            });
    }
}
```

## Anomaly Detection

Detecting anomalies in time series data.

```csharp
public class TimeSeriesData(DateTime Timestamp, float Value)
{
    public DateTime Timestamp { get; } = Timestamp;
    public float Value { get; } = Value;
}

public class AnomalyPrediction
{
    [VectorType(3)]
    public double[] Prediction { get; set; } = [];
}

public class AnomalyDetectionService(MLContext mlContext)
{
    public ITransformer TrainSpikeDetector(IDataView data, int pvalueHistoryLength = 30)
    {
        return mlContext.Transforms.DetectIidSpike(
            outputColumnName: nameof(AnomalyPrediction.Prediction),
            inputColumnName: nameof(TimeSeriesData.Value),
            confidence: 95.0,
            pvalueHistoryLength: pvalueHistoryLength)
            .Fit(data);
    }

    public ITransformer TrainChangePointDetector(IDataView data)
    {
        return mlContext.Transforms.DetectIidChangePoint(
            outputColumnName: nameof(AnomalyPrediction.Prediction),
            inputColumnName: nameof(TimeSeriesData.Value),
            confidence: 95.0,
            changeHistoryLength: 15)
            .Fit(data);
    }

    public IEnumerable<(DateTime Timestamp, bool IsAnomaly, double Score)> DetectAnomalies(
        ITransformer model,
        IDataView data)
    {
        var transformedData = model.Transform(data);

        var timestamps = mlContext.Data
            .CreateEnumerable<TimeSeriesData>(data, reuseRowObject: false)
            .Select(d => d.Timestamp)
            .ToList();

        var predictions = mlContext.Data
            .CreateEnumerable<AnomalyPrediction>(transformedData, reuseRowObject: false)
            .ToList();

        for (var i = 0; i < timestamps.Count; i++)
        {
            var prediction = predictions[i].Prediction;
            var isAnomaly = prediction[0] == 1;
            var score = prediction[1];

            yield return (timestamps[i], isAnomaly, score);
        }
    }
}

// Usage
public class AnomalyDetectionExample
{
    public void Run()
    {
        var mlContext = new MLContext();
        var service = new AnomalyDetectionService(mlContext);

        // Generate sample data with anomalies
        var data = GenerateTimeSeriesData();
        var dataView = mlContext.Data.LoadFromEnumerable(data);

        var model = service.TrainSpikeDetector(dataView);
        var anomalies = service.DetectAnomalies(model, dataView);

        foreach (var (timestamp, isAnomaly, score) in anomalies.Where(a => a.IsAnomaly))
        {
            Console.WriteLine($"Anomaly at {timestamp:g}: Score = {score:F4}");
        }
    }

    private static IEnumerable<TimeSeriesData> GenerateTimeSeriesData()
    {
        var random = new Random(42);
        var baseTime = DateTime.Now.AddDays(-100);

        for (var i = 0; i < 100; i++)
        {
            var value = 100 + (float)(Math.Sin(i * 0.1) * 10) + (float)(random.NextDouble() * 5);

            // Inject anomalies
            if (i == 25 || i == 75)
            {
                value += 50;
            }

            yield return new TimeSeriesData(baseTime.AddDays(i), value);
        }
    }
}
```

## Recommendation Engine

Product recommendations using matrix factorization.

```csharp
public class ProductRating(uint UserId, uint ProductId, float Rating)
{
    [LoadColumn(0)]
    public uint UserId { get; } = UserId;

    [LoadColumn(1)]
    public uint ProductId { get; } = ProductId;

    [LoadColumn(2)]
    public float Rating { get; } = Rating;
}

public class RatingPrediction
{
    public float Score { get; set; }
}

public class RecommendationService(MLContext mlContext)
{
    public ITransformer TrainModel(IDataView data)
    {
        var split = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

        var pipeline = mlContext.Recommendation().Trainers.MatrixFactorization(
            labelColumnName: nameof(ProductRating.Rating),
            matrixColumnIndexColumnName: nameof(ProductRating.UserId),
            matrixRowIndexColumnName: nameof(ProductRating.ProductId),
            numberOfIterations: 20,
            approximationRank: 100);

        var model = pipeline.Fit(split.TrainSet);

        // Evaluate
        var predictions = model.Transform(split.TestSet);
        var metrics = mlContext.Regression.Evaluate(
            predictions,
            labelColumnName: nameof(ProductRating.Rating),
            scoreColumnName: "Score");

        Console.WriteLine($"R-Squared: {metrics.RSquared:F4}");
        Console.WriteLine($"RMSE: {metrics.RootMeanSquaredError:F4}");

        return model;
    }

    public float PredictRating(ITransformer model, uint userId, uint productId)
    {
        var engine = mlContext.Model.CreatePredictionEngine<ProductRating, RatingPrediction>(model);
        return engine.Predict(new ProductRating(userId, productId, 0)).Score;
    }

    public IEnumerable<(uint ProductId, float PredictedRating)> GetTopRecommendations(
        ITransformer model,
        uint userId,
        IEnumerable<uint> candidateProducts,
        int topN = 10)
    {
        var engine = mlContext.Model.CreatePredictionEngine<ProductRating, RatingPrediction>(model);

        return candidateProducts
            .Select(productId => (
                ProductId: productId,
                PredictedRating: engine.Predict(new ProductRating(userId, productId, 0)).Score))
            .OrderByDescending(x => x.PredictedRating)
            .Take(topN);
    }
}
```

## Customer Segmentation (Clustering)

Grouping customers based on behavior patterns.

```csharp
public class CustomerData(
    float TotalPurchases,
    float AverageOrderValue,
    float DaysSinceLastPurchase,
    float PurchaseFrequency)
{
    public float TotalPurchases { get; } = TotalPurchases;
    public float AverageOrderValue { get; } = AverageOrderValue;
    public float DaysSinceLastPurchase { get; } = DaysSinceLastPurchase;
    public float PurchaseFrequency { get; } = PurchaseFrequency;
}

public class ClusterPrediction
{
    [ColumnName("PredictedLabel")]
    public uint ClusterId { get; set; }

    [ColumnName("Score")]
    public float[] Distances { get; set; } = [];
}

public class CustomerSegmentationService(MLContext mlContext)
{
    public ITransformer TrainModel(IDataView data, int numberOfClusters = 4)
    {
        var pipeline = mlContext.Transforms.Concatenate(
            "Features",
            nameof(CustomerData.TotalPurchases),
            nameof(CustomerData.AverageOrderValue),
            nameof(CustomerData.DaysSinceLastPurchase),
            nameof(CustomerData.PurchaseFrequency))
            .Append(mlContext.Transforms.NormalizeMinMax("Features"))
            .Append(mlContext.Clustering.Trainers.KMeans(
                featureColumnName: "Features",
                numberOfClusters: numberOfClusters));

        return pipeline.Fit(data);
    }

    public uint PredictCluster(ITransformer model, CustomerData customer)
    {
        var engine = mlContext.Model.CreatePredictionEngine<CustomerData, ClusterPrediction>(model);
        return engine.Predict(customer).ClusterId;
    }

    public Dictionary<uint, List<CustomerData>> SegmentCustomers(
        ITransformer model,
        IEnumerable<CustomerData> customers)
    {
        var engine = mlContext.Model.CreatePredictionEngine<CustomerData, ClusterPrediction>(model);

        return customers
            .GroupBy(c => engine.Predict(c).ClusterId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}

// Usage with cluster analysis
public class CustomerSegmentationExample
{
    public void Run()
    {
        var mlContext = new MLContext(seed: 42);
        var service = new CustomerSegmentationService(mlContext);

        var customers = GenerateSampleCustomers();
        var data = mlContext.Data.LoadFromEnumerable(customers);

        var model = service.TrainModel(data, numberOfClusters: 4);
        var segments = service.SegmentCustomers(model, customers);

        foreach (var (clusterId, clusterCustomers) in segments)
        {
            Console.WriteLine($"\nCluster {clusterId}: {clusterCustomers.Count} customers");
            Console.WriteLine($"  Avg Total Purchases: {clusterCustomers.Average(c => c.TotalPurchases):F2}");
            Console.WriteLine($"  Avg Order Value: {clusterCustomers.Average(c => c.AverageOrderValue):F2}");
        }
    }

    private static List<CustomerData> GenerateSampleCustomers()
    {
        var random = new Random(42);
        var customers = new List<CustomerData>();

        // High-value frequent buyers
        for (var i = 0; i < 50; i++)
        {
            customers.Add(new CustomerData(
                (float)(5000 + random.NextDouble() * 5000),
                (float)(200 + random.NextDouble() * 100),
                (float)(1 + random.NextDouble() * 7),
                (float)(20 + random.NextDouble() * 10)));
        }

        // Low-value occasional buyers
        for (var i = 0; i < 100; i++)
        {
            customers.Add(new CustomerData(
                (float)(100 + random.NextDouble() * 500),
                (float)(20 + random.NextDouble() * 30),
                (float)(30 + random.NextDouble() * 60),
                (float)(1 + random.NextDouble() * 3)));
        }

        return customers;
    }
}
```

## Fraud Detection

Real-time fraud detection with probability scoring.

```csharp
public class TransactionData(
    float Amount,
    float HourOfDay,
    float DayOfWeek,
    float DistanceFromHome,
    float DistanceFromLastTransaction,
    float RatioToMedianPurchase,
    bool IsFraud)
{
    public float Amount { get; } = Amount;
    public float HourOfDay { get; } = HourOfDay;
    public float DayOfWeek { get; } = DayOfWeek;
    public float DistanceFromHome { get; } = DistanceFromHome;
    public float DistanceFromLastTransaction { get; } = DistanceFromLastTransaction;
    public float RatioToMedianPurchase { get; } = RatioToMedianPurchase;

    [ColumnName("Label")]
    public bool IsFraud { get; } = IsFraud;
}

public class FraudPrediction
{
    [ColumnName("PredictedLabel")]
    public bool IsFraud { get; set; }

    public float Probability { get; set; }

    public float Score { get; set; }
}

public class FraudDetectionService(MLContext mlContext)
{
    public ITransformer TrainModel(IDataView data)
    {
        var split = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

        var pipeline = mlContext.Transforms.Concatenate(
            "Features",
            nameof(TransactionData.Amount),
            nameof(TransactionData.HourOfDay),
            nameof(TransactionData.DayOfWeek),
            nameof(TransactionData.DistanceFromHome),
            nameof(TransactionData.DistanceFromLastTransaction),
            nameof(TransactionData.RatioToMedianPurchase))
            .Append(mlContext.Transforms.NormalizeMinMax("Features"))
            .Append(mlContext.BinaryClassification.Trainers.FastTree(
                labelColumnName: "Label",
                featureColumnName: "Features",
                numberOfLeaves: 20,
                numberOfTrees: 100,
                minimumExampleCountPerLeaf: 10));

        var model = pipeline.Fit(split.TrainSet);

        // Evaluate with focus on precision/recall for fraud cases
        var predictions = model.Transform(split.TestSet);
        var metrics = mlContext.BinaryClassification.Evaluate(predictions, "Label");

        Console.WriteLine($"Accuracy: {metrics.Accuracy:P2}");
        Console.WriteLine($"AUC: {metrics.AreaUnderRocCurve:P2}");
        Console.WriteLine($"Precision (fraud): {metrics.PositivePrecision:P2}");
        Console.WriteLine($"Recall (fraud): {metrics.PositiveRecall:P2}");
        Console.WriteLine($"F1 Score: {metrics.F1Score:P2}");

        return model;
    }

    public FraudPrediction EvaluateTransaction(ITransformer model, TransactionData transaction)
    {
        var engine = mlContext.Model.CreatePredictionEngine<TransactionData, FraudPrediction>(model);
        return engine.Predict(transaction);
    }

    public (bool ShouldBlock, string Reason) MakeDecision(FraudPrediction prediction, float threshold = 0.7f)
    {
        if (prediction.Probability >= threshold)
        {
            return (true, $"High fraud probability: {prediction.Probability:P2}");
        }

        if (prediction.Probability >= 0.5f)
        {
            return (false, $"Medium risk - requires manual review: {prediction.Probability:P2}");
        }

        return (false, "Transaction approved");
    }
}
```

## Text Classification (Multi-Class)

Categorizing support tickets into departments.

```csharp
public class SupportTicket(string Description, string Department)
{
    public string Description { get; } = Description;

    [ColumnName("Label")]
    public string Department { get; } = Department;
}

public class TicketPrediction
{
    [ColumnName("PredictedLabel")]
    public string Department { get; set; } = string.Empty;

    public float[] Score { get; set; } = [];
}

public class TicketClassificationService(MLContext mlContext)
{
    public ITransformer TrainModel(IDataView data)
    {
        var split = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

        var pipeline = mlContext.Transforms.Conversion
            .MapValueToKey("Label")
            .Append(mlContext.Transforms.Text.FeaturizeText(
                "Features",
                nameof(SupportTicket.Description)))
            .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                labelColumnName: "Label",
                featureColumnName: "Features"))
            .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

        var model = pipeline.Fit(split.TrainSet);

        // Evaluate
        var predictions = model.Transform(split.TestSet);
        var metrics = mlContext.MulticlassClassification.Evaluate(predictions, "Label");

        Console.WriteLine($"Macro Accuracy: {metrics.MacroAccuracy:P2}");
        Console.WriteLine($"Micro Accuracy: {metrics.MicroAccuracy:P2}");
        Console.WriteLine($"Log Loss: {metrics.LogLoss:F4}");

        // Print per-class metrics
        Console.WriteLine("\nPer-class metrics:");
        foreach (var (className, classMetrics) in metrics.PerClassLogLoss.Select((v, i) => (i, v)))
        {
            Console.WriteLine($"  Class {className}: Log Loss = {classMetrics:F4}");
        }

        return model;
    }

    public TicketPrediction ClassifyTicket(ITransformer model, string description)
    {
        var engine = mlContext.Model.CreatePredictionEngine<SupportTicket, TicketPrediction>(model);
        return engine.Predict(new SupportTicket(description, string.Empty));
    }

    public IEnumerable<(string Department, float Confidence)> GetTopPredictions(
        ITransformer model,
        string description,
        string[] departments,
        int topN = 3)
    {
        var prediction = ClassifyTicket(model, description);

        return departments
            .Zip(prediction.Score, (dept, score) => (Department: dept, Confidence: score))
            .OrderByDescending(x => x.Confidence)
            .Take(topN);
    }
}
```

## Object Detection Integration

Using ONNX models for object detection.

```csharp
public class ObjectDetectionInput(byte[] Image)
{
    [ColumnName("image")]
    [ImageType(416, 416)]
    public byte[] Image { get; } = Image;
}

public class ObjectDetectionOutput
{
    [ColumnName("detected_boxes")]
    public float[] DetectedBoxes { get; set; } = [];

    [ColumnName("detected_classes")]
    public long[] DetectedClasses { get; set; } = [];

    [ColumnName("detected_scores")]
    public float[] DetectedScores { get; set; } = [];
}

public record DetectedObject(string ClassName, float Confidence, float X, float Y, float Width, float Height);

public class ObjectDetectionService(MLContext mlContext, string onnxModelPath, string[] classNames)
{
    public ITransformer LoadModel()
    {
        var pipeline = mlContext.Transforms.ApplyOnnxModel(
            modelFile: onnxModelPath,
            outputColumnNames: ["detected_boxes", "detected_classes", "detected_scores"],
            inputColumnNames: ["image"]);

        var emptyData = mlContext.Data.LoadFromEnumerable(Array.Empty<ObjectDetectionInput>());
        return pipeline.Fit(emptyData);
    }

    public IEnumerable<DetectedObject> DetectObjects(
        ITransformer model,
        byte[] imageBytes,
        float confidenceThreshold = 0.5f)
    {
        var engine = mlContext.Model.CreatePredictionEngine<ObjectDetectionInput, ObjectDetectionOutput>(model);
        var output = engine.Predict(new ObjectDetectionInput(imageBytes));

        var detections = new List<DetectedObject>();

        for (var i = 0; i < output.DetectedScores.Length; i++)
        {
            if (output.DetectedScores[i] < confidenceThreshold)
            {
                continue;
            }

            var classIndex = (int)output.DetectedClasses[i];
            var className = classIndex < classNames.Length ? classNames[classIndex] : $"class_{classIndex}";

            var boxIndex = i * 4;
            detections.Add(new DetectedObject(
                className,
                output.DetectedScores[i],
                output.DetectedBoxes[boxIndex],
                output.DetectedBoxes[boxIndex + 1],
                output.DetectedBoxes[boxIndex + 2],
                output.DetectedBoxes[boxIndex + 3]));
        }

        return detections;
    }
}
```

## AutoML for Model Selection

Using AutoML to automatically select the best model.

```csharp
public class AutoMLService(MLContext mlContext, uint maxExperimentTimeInSeconds = 600)
{
    public ExperimentResult<RegressionMetrics> RunRegressionExperiment(
        IDataView data,
        string labelColumnName)
    {
        var settings = new RegressionExperimentSettings
        {
            MaxExperimentTimeInSeconds = maxExperimentTimeInSeconds,
            OptimizingMetric = RegressionMetric.RSquared
        };

        var experiment = mlContext.Auto().CreateRegressionExperiment(settings);
        return experiment.Execute(data, labelColumnName);
    }

    public ExperimentResult<BinaryClassificationMetrics> RunBinaryClassificationExperiment(
        IDataView data,
        string labelColumnName)
    {
        var settings = new BinaryExperimentSettings
        {
            MaxExperimentTimeInSeconds = maxExperimentTimeInSeconds,
            OptimizingMetric = BinaryClassificationMetric.Accuracy
        };

        var experiment = mlContext.Auto().CreateBinaryClassificationExperiment(settings);
        return experiment.Execute(data, labelColumnName);
    }

    public ExperimentResult<MulticlassClassificationMetrics> RunMulticlassExperiment(
        IDataView data,
        string labelColumnName)
    {
        var settings = new MulticlassExperimentSettings
        {
            MaxExperimentTimeInSeconds = maxExperimentTimeInSeconds,
            OptimizingMetric = MulticlassClassificationMetric.MicroAccuracy
        };

        var experiment = mlContext.Auto().CreateMulticlassClassificationExperiment(settings);
        return experiment.Execute(data, labelColumnName);
    }

    public void PrintExperimentResults<TMetrics>(ExperimentResult<TMetrics> result)
    {
        Console.WriteLine($"Best run: {result.BestRun.TrainerName}");
        Console.WriteLine($"Runtime: {result.BestRun.RuntimeInSeconds:F2}s");
        Console.WriteLine($"Metrics: {result.BestRun.ValidationMetrics}");

        Console.WriteLine("\nAll runs:");
        foreach (var run in result.RunDetails.OrderByDescending(r => r.RuntimeInSeconds))
        {
            Console.WriteLine($"  {run.TrainerName}: {run.RuntimeInSeconds:F2}s");
        }
    }
}

// Usage
public class AutoMLExample
{
    public void Run()
    {
        var mlContext = new MLContext(seed: 42);
        var service = new AutoMLService(mlContext, maxExperimentTimeInSeconds: 300);

        // Load data
        var data = mlContext.Data.LoadFromTextFile<HouseData>("housing.csv", hasHeader: true);

        // Run AutoML experiment
        var result = service.RunRegressionExperiment(data, nameof(HouseData.Price));

        service.PrintExperimentResults(result);

        // Use the best model
        var bestModel = result.BestRun.Model;
        mlContext.Model.Save(bestModel, data.Schema, "best_model.zip");
    }
}
```

## Batch Prediction Pipeline

Processing large datasets efficiently with batch predictions.

```csharp
public class BatchPredictionService(MLContext mlContext, string modelPath)
{
    public async IAsyncEnumerable<TOutput> PredictBatchAsync<TInput, TOutput>(
        IEnumerable<TInput> inputs,
        int batchSize = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TInput : class
        where TOutput : class, new()
    {
        using var stream = File.OpenRead(modelPath);
        var model = mlContext.Model.Load(stream, out _);
        var engine = mlContext.Model.CreatePredictionEngine<TInput, TOutput>(model);

        var batch = new List<TInput>(batchSize);

        foreach (var input in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            batch.Add(input);

            if (batch.Count >= batchSize)
            {
                foreach (var item in batch)
                {
                    yield return engine.Predict(item);
                }
                batch.Clear();

                // Allow other operations
                await Task.Yield();
            }
        }

        // Process remaining items
        foreach (var item in batch)
        {
            yield return engine.Predict(item);
        }
    }

    public void PredictAndSave<TInput, TOutput>(
        IDataView inputData,
        string outputPath)
        where TInput : class
        where TOutput : class, new()
    {
        using var stream = File.OpenRead(modelPath);
        var model = mlContext.Model.Load(stream, out _);

        var predictions = model.Transform(inputData);

        using var outputStream = File.Create(outputPath);
        mlContext.Data.SaveAsText(predictions, outputStream, separatorChar: ',', headerRow: true);
    }
}
```
