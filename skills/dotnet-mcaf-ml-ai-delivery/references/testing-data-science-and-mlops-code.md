# Testing Data Science and MLOps Code

Testing ML code follows the same rule as the rest of MCAF: verify real behaviour with real boundaries.

## Core Rules

- test data pipelines, feature transforms, training code, and inference code through real execution paths
- do not use mocks, fakes, stubs, or service doubles
- do not hardcode important values inline; keep reusable test values in named constants or fixtures
- keep datasets tiny, deterministic, and representative
- separate fast checks from heavier end-to-end or training checks

## Data Loading

Use small real files or checked-in fixtures instead of replacing file I/O with doubles.

Recommended pattern:

1. keep a tiny CSV, JSON, image, or parquet fixture in the test assets
2. load it through the same public function or pipeline used by production code
3. assert the parsed schema, row count, key values, and validation behaviour

Good checks:

- valid file loads successfully
- missing file fails in the expected way
- invalid schema is rejected clearly
- nulls, outliers, or malformed records are handled explicitly

## Data Transformation

Transformation tests should prove input-to-output behaviour with small deterministic examples.

Use:

- named fixture constants for sample values
- one assertion focus per test when possible
- parametrized tests for shape, range, normalization, encoding, or padding rules

Avoid:

- inline magic numbers or string literals repeated across tests
- giant synthetic datasets that make failures unreadable

## Model Training and Inference

Use tiny real models or reduced training configurations that still execute the true code path.

Examples of useful checks:

- the model accepts the expected input shape
- the training step updates weights or parameters
- inference produces the expected output shape and type
- the saved model can be loaded and used by the real inference entry point
- evaluation code calculates metrics correctly on a fixed fixture dataset

Keep long-running verification separate from the fast inner loop by marking or grouping those suites explicitly.

## Integration and Pipeline Tests

For MLOps flows, prefer narrow but real integration coverage:

- real feature pipeline plus real model inference
- real model artifact load plus API or batch entry point
- real data validation and schema enforcement
- real persistence of model metadata, metrics, or lineage where the system depends on it

If a dependency is external, use a real sandbox or test environment with the real contract.

## Validation and Monitoring

ML verification should cover more than code execution.

Test or document:

- schema validation
- feature drift detection logic
- threshold and alert calculations
- fallback behaviour when model output is invalid or unavailable
- reproducibility of evaluation inputs and metrics

## Test Design Rules

- prefer TDD for bug fixes and non-trivial behaviour changes
- use named constants for file names, labels, thresholds, and expected values
- keep fixtures small enough to understand at a glance
- prove behaviour through public interfaces, not internal implementation details
- if a test is hard to write without doubles, treat that as a design problem and simplify the boundary
