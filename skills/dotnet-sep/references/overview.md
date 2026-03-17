# Sep references

## Primary sources

- [GitHub repository](https://github.com/nietras/Sep)
- [NuGet package (`Sep`)](https://www.nuget.org/packages/Sep/)
- [Main README](https://github.com/nietras/Sep/blob/main/README.md)
- [RFC 4180 (CSV baseline)](https://www.ietf.org/rfc/rfc4180.txt)
- [API and options overview in README](https://github.com/nietras/Sep#application-programming-interface-api)
- [Async support section](https://github.com/nietras/Sep#async-support)
- [SepReader options section](https://github.com/nietras/Sep#sepreaderoptions)
- [SepWriter options section](https://github.com/nietras/Sep#sepwriteroptions)

## Notes to use in routing

- `Sep` is a `.NET`-focused separator parser/writer emphasizing zero-allocation and performance.
- It supports explicit reader/writer option control and async/value-oriented APIs for high-throughput scenarios.
- Ref struct-based row/column types provide low-allocation access patterns; confirm this fits your code model before adopting in every consumer path.
- `Sep` is frequently compared with `CsvHelper` and other readers in project benchmarks; use for performance-sensitive workloads, not as a universal drop-in replacement.
