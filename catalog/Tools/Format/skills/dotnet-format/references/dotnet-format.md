# dotnet format

## Open/Free Status

- first-party .NET tooling
- free to use with the .NET SDK

## Install

Usually no extra install is needed beyond the .NET SDK because `dotnet format` is an SDK command.

If the repo intentionally pins an older standalone tool, follow the repo's existing tool manifest instead of inventing a new installation path.

## Verify First

Before changing install guidance, verify that the command already works:

```bash
dotnet format -h
```

## Common Commands

```bash
dotnet format MySolution.sln
dotnet format MySolution.sln --verify-no-changes
dotnet format whitespace MySolution.sln --verify-no-changes
dotnet format style MySolution.sln --verify-no-changes
dotnet format analyzers MySolution.sln --verify-no-changes
```

## CI Fit

- use `--verify-no-changes` as the default gate
- keep `.editorconfig` in repo and versioned
- do not pair `dotnet format` and `CSharpier` without explicit ownership

## When Not To Use

- when the repo intentionally standardized on `CSharpier` as the sole formatter for C# and XML
- when the target is not an SDK-style .NET solution or project

## Sources

- [dotnet format command](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format)
