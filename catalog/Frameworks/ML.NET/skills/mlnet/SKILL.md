---
name: mlnet
description: "Use ML.NET to train, evaluate, or integrate machine-learning models into .NET applications with realistic data preparation, inference, and deployment expectations."
compatibility: "Requires ML.NET, Model Builder, or ML.NET CLI scenarios."
---

# ML.NET

## Trigger On

- integrating machine learning into a .NET application
- training or retraining ML.NET models from local data
- reviewing inference pipelines, model loading, or AutoML-generated code

## Workflow

1. Start from the prediction task and data quality, not the algorithm or package list.
2. Separate training code from inference code so the production path stays lean and predictable.
3. Review feature engineering, normalization, label quality, and evaluation metrics before trusting model output.
4. Use Model Builder or the ML.NET CLI when they speed up exploration, but inspect the generated C# before treating it as production architecture.
5. Plan how the model is loaded, versioned, and refreshed in the application lifecycle.
6. Validate with representative datasets and explicit evaluation, not only with a sample that happens to run.

## Deliver

- ML.NET pipelines that fit the prediction task
- production-usable inference integration
- evaluation evidence tied to the business scenario

## Validate

- model quality is measured, not assumed
- training and inference responsibilities are separated
- deployment and versioning expectations are explicit

## References

- [patterns.md](references/patterns.md) - Data loading, training pipelines, evaluation metrics, deployment strategies, and feature engineering patterns
- [examples.md](references/examples.md) - Complete examples for sentiment analysis, price prediction, image classification, anomaly detection, recommendations, clustering, fraud detection, text classification, object detection, and AutoML
