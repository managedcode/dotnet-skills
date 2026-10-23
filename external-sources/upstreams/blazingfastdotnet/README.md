# Blazing Fast .NET

Tips & tricks to tune .NET 10.0 - 11.0 workloads for better performance (mostly targeting benchmarks), packaged as a single [`SKILL.md`](SKILL.md) for AI coding agents. It reads fine as a regular doc too.

## Usage

I don't expect it to be useful as an always-on skill. It's rather something to consult on demand when you need to optimize a .NET workload, e.g.:

```
Hey, consult https://raw.githubusercontent.com/EgorBo/BlazingFastDotnet/main/SKILL.md to go brrrr
```

> [!NOTE]
> This is not an official Microsoft guide or documentation. Use at your own discretion and always measure performance changes.

## Not welcome in this doc

- Anything that requires an `unsafe` context, under either the unsafe-v1 or the unsafe-v2 rules.
- `ArrayPool<T>`: renting and returning buffers by hand is too error-prone, and I don't want to be responsible for the resulting use-after-return and double-return bugs.
- Third-party dependencies: I'd like to minimize number of dependencies, currently it's limitted by:
  - [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet)
  - [FlameGraph](https://github.com/brendangregg/FlameGraph) (for `perf` flame graphs)
