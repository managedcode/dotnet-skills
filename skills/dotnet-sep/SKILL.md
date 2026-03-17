---
name: dotnet-sep
version: "1.0.0"
category: "Data"
description: "Use Sep for high-performance separated-value parsing and writing with low allocations, control over escaping/trimming behavior, and AOT-friendly runtime performance in .NET."
compatibility: "Requires a .NET project that can reference the `Sep` package and accept ref struct/span-oriented read/write patterns for row and column access."
---

# Sep for separated values

## Trigger On

- project needs fast CSV/TSV/other delimiter parsing or writing and current solution is allocation-heavy
- stream-based ingestion or ML feature extraction over large delimited files is a key path
- explicit control over separator, quoting, escaping, trimming, or output round-tripping is required
- NativeAOT, trimming, or startup performance is a hard requirement
- parser replacement from other CSV libraries is being evaluated

## Workflow

1. Confirm the format contract (delimiter, header behavior, quoting, line endings, and data types).
2. Pick reader/writer construction mode:
   - inferred separator: default `Sep.Reader()` behavior when header/format allows inference,
   - explicit separator for strict schemas: `Sep.New(',').Reader()` / `Sep.New(';').Writer()`.
3. Configure options only where required (`SepReaderOptions` / `SepWriterOptions`) to avoid hidden transformations.
4. Process rows through the span-first APIs and parse to target types through `ISpanParsable<T>` where possible.
5. Use `writer` on the same `SepSpec` when round-tripping to preserve separator and culture.
6. Benchmark against current pipeline before adopting in hot paths.
7. If parser behavior must support LINQ/collection materialization heavily, validate whether ref-struct row types match the downstream assumptions.

```mermaid
flowchart LR
  A["Delimited input file/stream/text"] --> B["Sep.Reader / ReaderOptions"]
  B --> C["Header + Row + Col access"]
  C --> D["Parse with Span / ISpanParsable"]
  D --> E["Transform / validate / map"]
  E --> F["SepWriter via Spec"]
  F --> G["Output file or text"]
```

## Deliver

- read/write usage plan with explicit separator and option policy
- recommendations for when Sep fits and when simpler parsers are safer
- concrete examples for low-allocation row/column processing and output generation

## Validate

- delimiter and quoting behavior is explicit and tested on real sample files
- parser selection is aligned with performance and memory budget
- AOT/trimming requirements are still satisfied after package/reference choices
- row/col access assumptions are compatible with `ref struct` flow constraints

## Load References

- [references/overview.md](references/overview.md) - canonical links and practical decision notes for the skill
