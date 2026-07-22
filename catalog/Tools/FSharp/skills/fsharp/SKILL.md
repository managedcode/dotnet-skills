---
name: fsharp
description: "Write, review, or modernize F# code in .NET repositories with functional-first design, algebraic data types, pattern matching, pipelines, async workflows, project ordering, and C# interop. USE FOR: F# .fs/.fsproj code; discriminated unions, records, options, results, pattern matching, computation expressions, or functional domain modeling; strongly typed AI-generated .NET code. DO NOT USE FOR: C# language modernization; FSI-only exploratory scripts with no project changes. INVOKES: inspect .fsproj file order and SDK settings, edit F# source and project files, and run dotnet build/test or dotnet fsi validation commands when changes are made."
compatibility: "Requires a .NET SDK with F# support and an F# project, script, or interop boundary."
---

# F# for .NET

## Trigger On

- the task touches `.fs`, `.fsx`, `.fsi`, or `.fsproj` files
- domain logic benefits from records, discriminated unions, options, results, or exhaustive pattern matching
- generated code needs a strongly typed functional model instead of nullable primitive state
- F# code must interoperate with C# or other .NET libraries
- the repository needs project setup, source ordering, or validation for F# code

## Do Not Use For

- C# language feature selection; use `modern-csharp`
- analyzer-only, formatter-only, or CI-only work with no F# language decisions
- one-off REPL or scripting exploration that does not affect project code; use `fsi`
- non-.NET functional languages

## Project Setup

Use the .NET SDK templates when adding F# projects.

```bash
dotnet new classlib -lang "F#" -o src/Domain
dotnet new console -lang "F#" -o src/App
dotnet new xunit -lang "F#" -o tests/Domain.Tests
dotnet sln add src/Domain/Domain.fsproj tests/Domain.Tests/Domain.Tests.fsproj
dotnet add tests/Domain.Tests/Domain.Tests.fsproj reference src/Domain/Domain.fsproj
dotnet build
```

`FSharp.Core` is normally supplied by the F# SDK project. Add or pin it explicitly only when the repo has a package policy that requires deterministic package versions.

```bash
dotnet add src/Domain/Domain.fsproj package FSharp.Core
```

## Source Ordering

F# compiles files in the order listed in the project file. Define types and modules before files that consume them.

```xml
<ItemGroup>
  <Compile Include="Domain.fs" />
  <Compile Include="Validation.fs" />
  <Compile Include="Program.fs" />
</ItemGroup>
```

When adding a file, update the `.fsproj` intentionally. Do not assume wildcard ordering.

## Workflow

1. Inspect the existing `.fsproj`, `.fs`, `.fsi`, and `.fsx` files to determine compile order, public boundaries, and whether the code is F#-first or C#-facing.
2. Choose the smallest F# model that represents the invariant: records for named product data, discriminated unions for closed state, `option` for absence, and `Result` for expected failures.
3. Add or edit source files in dependency order, then update the `.fsproj` compile list before consumers reference the new code.
4. Review public API shape for .NET interop. Translate F#-specific internal models to DTOs, methods, or `Try*` patterns when C# callers need a stable surface.
5. Run `dotnet build`, targeted tests, and any relevant `dotnet fsi` probes before returning the final result.

## Current Upstream Notes

- The July 2026 F# overview refresh continues to emphasize functional-first programming on .NET with records, discriminated unions, pattern matching, units of measure, type providers, and interop. Keep F# guidance domain-model oriented rather than translating C# object patterns mechanically.
- Use `fsi` for exploratory scripts, but move durable code into ordered `.fsproj` files before it becomes production behavior.

## Practical Patterns

### Model State With Records And Unions

Prefer a type that names every valid state over loose strings, nullable values, or parallel booleans.

```fsharp
module Orders

type OrderId = private OrderId of string

module OrderId =
    let tryCreate value =
        if System.String.IsNullOrWhiteSpace value then
            Error "Order id is required."
        else
            Ok (OrderId value)

type Payment =
    | Card of last4: string
    | Wire of iban: string
    | PurchaseOrder of number: string

type Order =
    { Id: OrderId
      Customer: string
      Payment: Payment }

let describePayment payment =
    match payment with
    | Card last4 -> $"card ending {last4}"
    | Wire iban -> $"wire transfer {iban}"
    | PurchaseOrder number -> $"purchase order {number}"
```

### Compose Validation With Result

Return `Result<'T,'Error>` when callers must handle failure and when failures are part of the domain.

```fsharp
module Pricing

type Price =
    private
    | Price of decimal

module Price =
    let tryCreate amount =
        if amount < 0m then
            Error "Price cannot be negative."
        else
            Ok (Price amount)

    let value (Price amount) = amount

type Line =
    { Sku: string
      Quantity: int
      UnitPrice: Price }

let tryCreateLine sku quantity amount =
    match Price.tryCreate amount with
    | Ok price when quantity > 0 ->
        Ok { Sku = sku; Quantity = quantity; UnitPrice = price }
    | Ok _ ->
        Error "Quantity must be positive."
    | Error message ->
        Error message
```

### Read And Transform Data

Use pipelines for readable transformations, but stop before the pipeline hides error handling or allocation cost.

```fsharp
open System.IO

let readActiveUsers path =
    File.ReadLines path
    |> Seq.skip 1
    |> Seq.choose (fun line ->
        match line.Split(',') with
        | [| id; name; "active" |] -> Some {| Id = id; Name = name |}
        | _ -> None)
    |> Seq.toList
```

### Write A Small .NET Boundary

Make public interop APIs easy for C# callers. Hide F#-specific details behind functions, methods, or DTO records when needed.

```fsharp
namespace Company.Domain

type InvoiceDto =
    { Id: string
      Total: decimal
      IsPaid: bool }

module Invoice =
    let markPaid invoice =
        { invoice with IsPaid = true }
```

## Interop Guidance

- Keep F# discriminated unions inside F# boundaries unless C# consumers are expected to understand their generated shape.
- Use records or explicit classes for public cross-language DTOs.
- Use `option` internally; translate to nullable annotations, `Try*` methods, or `Result` at C#-first public boundaries.
- Avoid throwing for expected domain failures. Use exceptions for unexpected infrastructure failures.
- Prefer `task { }` or `Task`-returning APIs at .NET interop boundaries; use `Async<'T>` when the code is F#-first.

## Review Checklist

- The `.fsproj` compile order matches dependency order.
- All union cases are handled explicitly; wildcard branches are justified.
- Public types have stable names and shapes for downstream .NET consumers.
- Domain failures are modeled with `option` or `Result`, not undocumented `null`.
- Pipelines remain readable and do not hide repeated enumeration of expensive sequences.
- Tests cover at least one success and one failure path for each domain constructor or validator.

## Validate

Run the narrowest relevant checks after changes:

```bash
dotnet build
dotnet test
dotnet fsi scripts/check.fsx
```

If adding a new F# file, also inspect the project file diff to confirm the compile order is deliberate.

## Sources

- https://learn.microsoft.com/dotnet/fsharp/what-is-fsharp
- https://learn.microsoft.com/dotnet/fsharp/get-started/get-started-command-line
- https://learn.microsoft.com/dotnet/fsharp/language-reference/discriminated-unions
- https://learn.microsoft.com/dotnet/fsharp/language-reference/pattern-matching
- https://learn.microsoft.com/dotnet/fsharp/language-reference/results
