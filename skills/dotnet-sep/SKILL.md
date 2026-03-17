---
name: dotnet-sep
version: "1.0.0"
category: "Data"
description: "Use Sep for high-performance separated-value parsing and writing in .NET, including delimiter inference, explicit parser/writer options, and low-allocation row/column workflows."
compatibility: "Requires a .NET project that can reference the `Sep` package and accept span/ref-struct row/column APIs for row-by-row processing."
---

# Sep for .NET separated values

## Trigger On

- delimited data needs are performance-sensitive and allocation-aware
- project needs explicit control over separator inference, escaping, trimming, and header behavior
- reading/writing large or long-lived file pipelines in ML, ETL, or analytics workloads
- startup/perf tests require AOT/trimming-friendly CSV/TSV processing

## Install

- NuGet:
  - `dotnet add package Sep`
  - `dotnet add package Sep --version <version>`
- XML package reference:
  - `<PackageReference Include="Sep" Version="x.y.z" />`
- Verify baseline support by checking the package page:
  - [NuGet: Sep](https://www.nuget.org/packages/Sep/)
- Source:
  - [GitHub: nietras/Sep](https://github.com/nietras/Sep)

## Workflow

```mermaid
flowchart LR
  A[Input source: file/text/stream] --> B[Sep.Reader or Sep.New(...).Reader]
  B --> C[SepReaderOptions]
  C --> D[Rows -> Cols -> Span/Parse]
  D --> E[Transform and validate]
  E --> F[SepWriter via SepWriterOptions]
  F --> G[To file/text output]
```

1. Decide schema shape
   - header present or no header
   - separator known (`;`, `,`, tab, custom) or infer from first row
   - row/column quoting rules
2. Build reader with `Sep.Reader(...)` and explicit options only where needed:
   - `Sep.Reader()` for inferred separator from header-like first row
   - `Sep.New(',').Reader(...)` for explicit separator mode
   - `Sep.Reader(o => o with { HasHeader = false })` if header is absent
3. Read rows and map columns as `ReadOnlySpan<char>` first, convert only when needed.
4. For output, use `reader.Spec.Writer()` when you need the same separator/culture as input.
5. Control writer behavior with `Sep.Writer(...)` and `SepWriterOptions` (`WriteHeader`, `Escape`, `DisableColCountCheck`).
6. Add async only where it brings value and your runtime is C# 13 / .NET 9+ for `await foreach` over async reader rows.
7. Use `ParallelEnumerate` for CPU-heavy transformations only after benchmarking single-threaded baseline.

### Install and read patterns

```csharp
using var reader = Sep.Reader(o => o with
{
    HasHeader = true,
    Unescape = true,
    Trim = SepTrim.Both
}).FromText(data);

foreach (var row in reader)
{
    var id = row["Id"].Parse<int>();
    var name = row[1].ToString();
    // process row
}
```

### Write patterns

```csharp
using var reader = Sep.Reader().FromFile("input.csv");
using var writer = reader.Spec.Writer().ToFile("output.csv");

foreach (var row in reader)
{
    using var writeRow = writer.NewRow(row);
    writeRow["Amount"].Format(row["Amount"].Parse<double>() * 1.2);
}
```

### Async reading and writing

```csharp
var text = "A;B\n1;hello\n";

using var reader = await Sep.Reader().FromTextAsync(text);
await using var writer = reader.Spec.Writer().ToText();

await foreach (var row in reader)
{
    await using var writeRow = writer.NewRow(row);
    var normalized = row["B"].ToString().ToUpperInvariant();
    writeRow["B"].Set(normalized);
}
```

### Common configuration patterns

- Header-driven read
  - default `HasHeader = true`
  - query by name: `row["ColName"]`
- Headerless pipelines
  - `HasHeader = false`
  - use index-based access: `row[0]`, `row[1]`
- Round-trip output
  - start writer with `reader.Spec.Writer()` to preserve inference and formatting contract
- Speed-first processing
  - keep default buffer + culture unless profiling proves a need to tune

## Best practices

- Parse to primitive types with `Parse<T>` in hot paths to avoid extra allocations.
- Keep `ToString`/format conversions at the edge (presentational layers), not in inner loops.
- Prefer `Unescape`, `Trim`, and `DisableQuotesParsing` settings deliberately and test with realistic samples.
- For large transforms, isolate heavy CPU work after enumeration and then apply `ParallelEnumerate` where appropriate.

## Limitations to check before production

- `SepReader.Row` and `SepWriter.Row` are `ref struct`s:
  - avoid patterns that store rows beyond immediate scope
  - materialize if you truly need random async/LINQ-style buffering
- `SepReader` row iteration is row-by-row by design; it is intentionally not the same as a classic collection model.

## Deliver

- installation and usage guide that is ready to copy into a .NET repo
- practical reader/writer configuration patterns
- clear notes on defaults, tradeoffs, and constraints

## Validate

- `dotnet add package Sep` installs correctly and project compiles
- one file-read sample and one file-write sample execute successfully
- header/no-header and explicit-separator cases are covered
- at least one validation sample for quoting/unescaping or async path exists if required by task

## Load References

- [references/overview.md](references/overview.md) - official links and practical decision notes.
