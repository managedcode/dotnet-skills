# Orleans Examples

Use this reference when the user needs an example-first entry point instead of conceptual guidance.

## Official Sample Hubs

- [Microsoft Learn Orleans samples browser](https://learn.microsoft.com/samples/browse/?expanded=dotnet&products=dotnet-orleans)
- [dotnet/samples Orleans directory](https://github.com/dotnet/samples/tree/main/orleans)
- [Orleans repository samples README](https://github.com/dotnet/orleans/blob/main/samples/README.md)

## Getting Started Examples

- [Build your first Orleans app](https://learn.microsoft.com/dotnet/orleans/quickstarts/build-your-first-orleans-app)
- [Hello, World sample](https://learn.microsoft.com/samples/dotnet/samples/orleans-hello-world-sample-app)
- [Visual Basic Hello World source](https://github.com/dotnet/samples/tree/main/orleans/VBHelloWorld/README.md)
- [F# Hello World source](https://github.com/dotnet/samples/tree/main/orleans/FSharpHelloWorld/README.md)

## Domain And Architecture Samples

- [Adventure game](https://learn.microsoft.com/samples/dotnet/samples/orleans-text-adventure-game)
- [Chirper social media sample](https://learn.microsoft.com/samples/dotnet/samples/orleans-chirper-social-media-sample-app)
- [GPS device tracker](https://learn.microsoft.com/samples/dotnet/samples/orleans-gps-device-tracker-sample)
- [Presence service](https://learn.microsoft.com/samples/dotnet/samples/orleans-gaming-presence-service-sample)
- [Tic Tac Toe web game](https://learn.microsoft.com/samples/dotnet/samples/orleans-tictactoe-web-based-game)
- [Stocks sample](https://learn.microsoft.com/samples/dotnet/samples/orleans-stocks-sample-app)

## Hosting And UI Samples

- [Deploy and scale an Orleans app on Azure](https://learn.microsoft.com/dotnet/orleans/quickstarts/deploy-scale-orleans-on-azure)
- [Voting app on Kubernetes](https://learn.microsoft.com/samples/dotnet/samples/orleans-voting-sample-app-on-kubernetes)
- [Blazor Server + Orleans](https://learn.microsoft.com/samples/dotnet/samples/orleans-aspnet-core-blazor-server-sample)
- [Blazor WebAssembly + Orleans](https://learn.microsoft.com/samples/dotnet/samples/orleans-aspnet-core-blazor-wasm-sample)
- [Transport Layer Security sample](https://learn.microsoft.com/samples/dotnet/samples/orleans-transport-layer-security-tls)

## Streams, Observers, And Real-Time

- [Chat Room sample](https://learn.microsoft.com/samples/dotnet/samples/orleans-chat-room-sample)
- [Streaming pub/sub with Azure Event Hubs](https://learn.microsoft.com/samples/dotnet/samples/orleans-streaming-pubsub-with-azure-event-hub)
- [Chirper social sample](https://learn.microsoft.com/samples/dotnet/samples/orleans-chirper-social-media-sample-app)
- [GPS Tracker with SignalR](https://learn.microsoft.com/samples/dotnet/samples/orleans-gps-device-tracker-sample)

## State, Persistence, And Transactions

- [Bank Account ACID transactions](https://learn.microsoft.com/samples/dotnet/samples/orleans-bank-account-acid-transactions)
- [Custom grain storage sample page](https://learn.microsoft.com/dotnet/orleans/tutorials-and-samples/custom-grain-storage)

## Testing

- [Unit testing documentation and example](https://learn.microsoft.com/dotnet/orleans/implementation/testing)

## Repo-Grounded Integration Harness Patterns

- `AIBase`: shared `AspireTestFixture` for API/UI suites plus `AIBaseTestApplication : WebApplicationFactory<...>` for direct DI and grain access
- `WA.Storied.Agents`: shared `AspireTestFixture` for AppHost lifecycle and Playwright, plus `AIBaseTestApplication : WebApplicationFactory<HostEntryPointMarker>` for Host services and Orleans runtime access
- Load `testing-patterns.md` when you need working snippets for shared AppHost fixtures, `WebApplicationFactory`, SignalR, or browser automation instead of standalone `InProcessTestCluster` examples

## Additional Community-Oriented Example Lists Mentioned By Official Samples

- [Road to Orleans](https://github.com/PiotrJustyna/road-to-orleans/)
- [HanBaoBao Kubernetes sample](https://github.com/ReubenBond/hanbaobao-web)

## Usage Guidance

- Pick the example that matches the dominant concern before reading broad docs.
- Use quickstarts for first wiring, tutorial pages for guided walkthroughs, and sample-browser entries for concrete repo layouts.
- Cross-check sample age and package names against the live Orleans docs when copying code into a modern project.
