# Serialization API Reference

Detailed serialization patterns from official Orleans documentation. Orleans uses two kinds of serialization: grain call serialization (between grains/clients) and grain storage serialization (persistence).

## Core Attributes

### `[GenerateSerializer]`

Required on all types passed between grains, stored in state, or used in streams.

```csharp
[GenerateSerializer]
public class PlayerState
{
    [Id(0)] public string Name { get; set; } = "";
    [Id(1)] public int Level { get; set; }
    [Id(2)] public List<string> Achievements { get; set; } = [];
}
```

### `[Id(N)]`

Stable member identification. Rules:
- Each serialized member needs a unique `[Id(N)]`
- Adding new `[Id]` members is safe (backward compatible)
- Removing members is safe if the `[Id]` is not reused
- Changing member types is a **breaking change**
- IDs are per-type, not global

### `[Alias("name")]`

Type aliases for safe renaming:

```csharp
[GenerateSerializer]
[Alias("player-state-v1")]
public class PlayerState { }
```

Allows type to be renamed without breaking deserialization.

### `[Immutable]`

Skip copy overhead for immutable types:

```csharp
[Immutable]
[GenerateSerializer]
public record SensorReading(
    [property: Id(0)] string SensorId,
    [property: Id(1)] double Value,
    [property: Id(2)] DateTime Timestamp);
```

Can also be applied per-parameter or per-property:

```csharp
Task ProcessData([Immutable] LargePayload data);
```

## Versioning Rules

| Change | Safe? |
|---|---|
| Add new `[Id]` member | Yes |
| Remove member (don't reuse ID) | Yes |
| Rename member (keep same ID) | Yes |
| Change member type | **No** |
| Rename type (with `[Alias]`) | Yes |
| Rename type (without `[Alias]`) | **No** |

## Surrogates

Serialize types you don't own:

```csharp
// Surrogate for DateTimeOffset
[GenerateSerializer]
public struct DateTimeOffsetSurrogate
{
    [Id(0)] public long Ticks;
    [Id(1)] public short OffsetMinutes;
}

[RegisterConverter]
public sealed class DateTimeOffsetConverter :
    IConverter<DateTimeOffset, DateTimeOffsetSurrogate>
{
    public DateTimeOffset ConvertFromSurrogate(in DateTimeOffsetSurrogate s) =>
        new(s.Ticks, TimeSpan.FromMinutes(s.OffsetMinutes));

    public DateTimeOffsetSurrogate ConvertToSurrogate(in DateTimeOffset value) =>
        new() { Ticks = value.Ticks, OffsetMinutes = (short)value.Offset.TotalMinutes };
}
```

## Copier

Orleans copies objects by default to prevent grain state corruption from accidental mutation.

```csharp
// Custom copier
[RegisterCopier]
public sealed class MyTypeCopier : IDeepCopier<MyType>
{
    public MyType DeepCopy(MyType input, CopyContext context) =>
        new MyType { Value = input.Value };
}
```

Use `[Immutable]` to skip copying entirely for types that are never mutated after creation.

## Grain Storage Serialization

Configurable per provider. Default uses `Newtonsoft.Json` for stored state.

```csharp
// Use Orleans native format
siloBuilder.AddRedisGrainStorage("redis", options =>
{
    options.GrainStorageSerializer = new OrleansGrainStorageSerializer(
        serializerSessionPool);
});

// Custom serializer
public class MySerializer : IGrainStorageSerializer
{
    public BinaryData Serialize<T>(T value) { }
    public T Deserialize<T>(BinaryData data) { }
}
```

## Serialization of Immutable Types

Immutable types skip the copy step, improving performance for read-heavy grain communication.

### Type-Level Immutability

```csharp
[Immutable]
[GenerateSerializer]
public record SensorReading(
    [property: Id(0)] string SensorId,
    [property: Id(1)] double Value,
    [property: Id(2)] DateTime Timestamp);
```

### Member-Level Immutability

```csharp
[GenerateSerializer]
public class MyGrainState
{
    [Id(0), Immutable] public IReadOnlyList<string> Tags { get; set; } = [];
    [Id(1)] public int MutableCount { get; set; }
}
```

### Parameter-Level Immutability

```csharp
public interface IMyGrain : IGrainWithStringKey
{
    Task ProcessData([Immutable] LargePayload data);
}
```

When `[Immutable]` is applied, Orleans trusts that the object will not be mutated after it's passed. Violating this contract can corrupt grain state.

## Configure Serialization

### Serializer Selection

Orleans uses its own high-performance serializer by default. Configuration options:

```csharp
// Use System.Text.Json for specific types
siloBuilder.Services.AddSerializer(builder =>
{
    builder.AddJsonSerializer(
        isSupported: type => type.Namespace?.StartsWith("MyApp.Dto") == true);
});

// Use Newtonsoft.Json for specific types
siloBuilder.Services.AddSerializer(builder =>
{
    builder.AddNewtonsoftJsonSerializer(
        isSupported: type => type.GetCustomAttribute<JsonObjectAttribute>() != null);
});
```

### External Serializer Packages

| Package | Serializer |
|---|---|
| `Microsoft.Orleans.Serialization.SystemTextJson` | System.Text.Json |
| `Microsoft.Orleans.Serialization.NewtonsoftJson` | Newtonsoft.Json |
| `Microsoft.Orleans.Serialization.MessagePack` | MessagePack |
| `Microsoft.Orleans.Serialization.Protobuf` | Protobuf |
| `Microsoft.Orleans.Serialization.FSharp` | F# types |

## Customize Serialization

### Custom Serializer

```csharp
[RegisterSerializer]
public sealed class MyTypeSerializer : IFieldCodec<MyType>
{
    public void WriteField<TBufferWriter>(ref Writer<TBufferWriter> writer,
        uint fieldIdDelta, Type expectedType, MyType value)
        where TBufferWriter : IBufferWriter<byte>
    {
        StringCodec.WriteField(ref writer, fieldIdDelta, value.ToString());
    }

    public MyType ReadValue<TInput>(ref Reader<TInput> reader, Field field)
    {
        return MyType.Parse(StringCodec.ReadValue(ref reader, field));
    }
}
```

### Custom Copier

```csharp
[RegisterCopier]
public sealed class MyTypeCopier : IDeepCopier<MyType>
{
    public MyType DeepCopy(MyType input, CopyContext context) =>
        new MyType(input.Value); // create independent copy
}
```

### Surrogates for External Types

For types you don't own, create a surrogate with converter:

```csharp
[GenerateSerializer]
public struct DateTimeOffsetSurrogate
{
    [Id(0)] public long Ticks;
    [Id(1)] public short OffsetMinutes;
}

[RegisterConverter]
public sealed class DateTimeOffsetConverter :
    IConverter<DateTimeOffset, DateTimeOffsetSurrogate>
{
    public DateTimeOffset ConvertFromSurrogate(in DateTimeOffsetSurrogate s) =>
        new(s.Ticks, TimeSpan.FromMinutes(s.OffsetMinutes));

    public DateTimeOffsetSurrogate ConvertToSurrogate(in DateTimeOffset value) =>
        new() { Ticks = value.Ticks, OffsetMinutes = (short)value.Offset.TotalMinutes };
}
```

### Grain Storage Serializer Override

```csharp
// Default: Newtonsoft.Json for stored state
// Override per provider:
siloBuilder.AddRedisGrainStorage("redis", options =>
{
    // Use Orleans native format instead of JSON
    options.GrainStorageSerializer = new OrleansGrainStorageSerializer(
        serializerSessionPool);
});

// Or implement custom
public class MyGrainStorageSerializer : IGrainStorageSerializer
{
    public BinaryData Serialize<T>(T value) { /* ... */ }
    public T Deserialize<T>(BinaryData data) { /* ... */ }
}
```

## Common Serialization Mistakes

| Mistake | Fix |
|---|---|
| Missing `[GenerateSerializer]` | Add to all grain state and message types |
| Missing `[Id(N)]` | Add unique IDs to all serialized members |
| Non-serializable fields in state | Mark with `[NonSerialized]` or inject via DI |
| Reusing `[Id]` after removal | Use a new unused ID number |
| Storing `HttpClient` / `Action` in state | Inject as service, not in state |
