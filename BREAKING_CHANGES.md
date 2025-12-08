# Breaking Changes

This document describes breaking changes introduced when migrating from the legacy `RateGate` implementation to the new `ApiRateLimiter` using .NET's built-in `System.Threading.RateLimiting` APIs.

## Version 2.0.0 (Rate Limiting Migration)

### New NuGet Dependency

The library now depends on `System.Threading.RateLimiting` package. This package is part of .NET 7+ and is available as a NuGet package for earlier versions.

### `ApiBase` Changes

#### `ApiBase` now implements `IDisposable` and `IAsyncDisposable`

**Breaking Change:** The `ApiBase` class now implements `IDisposable` and `IAsyncDisposable` interfaces to properly dispose of the internal `ApiRateLimiter`.

**Migration:** If you have classes that inherit from `ApiBase`, ensure they are properly disposed when no longer needed:

```csharp
// Before
var api = new MyApi(config, httpClientFactory);
await api.SomeMethod();

// After - Option 1: using statement
using var api = new MyApi(config, httpClientFactory);
await api.SomeMethod();

// After - Option 2: explicit disposal
var api = new MyApi(config, httpClientFactory);
try
{
    await api.SomeMethod();
}
finally
{
    await api.DisposeAsync();
}
```

### `RateGate` Class Deprecation

**Breaking Change:** The `RateGate` class is now marked as `[Obsolete]`. While it remains functional for backwards compatibility, it will generate compiler warnings.

**Migration:** Replace usage of `RateGate` with `ApiRateLimiter`:

```csharp
// Before
var rateGate = new RateGate(RateLimit.Build(10, TimeSpan.FromSeconds(1)));
rateGate.WaitToProceed();
rateGate.Dispose();

// After
var rateLimiter = new ApiRateLimiter(RateLimit.Build(10, TimeSpan.FromSeconds(1)));
rateLimiter.WaitToProceed();
rateLimiter.Dispose();

// Or using the factory method
var rateLimiter = RateLimit.Build(10, TimeSpan.FromSeconds(1)).CreateRateLimiter();
```

### New Rate Limiting API

#### New `ApiRateLimiter` Class

A new `ApiRateLimiter` class has been introduced that wraps the built-in `System.Threading.RateLimiting.SlidingWindowRateLimiter`. It provides:

- `WaitToProceed()` - Synchronous blocking wait
- `WaitToProceed(int millisecondsTimeout)` - Synchronous wait with timeout
- `WaitToProceed(TimeSpan timeout)` - Synchronous wait with timeout
- `WaitToProceedAsync(CancellationToken)` - Asynchronous wait (NEW)
- `WaitToProceedAsync(TimeSpan, CancellationToken)` - Asynchronous wait with timeout (NEW)
- `NotifyPriorityCallMade()` - For high-priority calls that bypass waiting
- `GetAvailablePermits()` - Get current available permit count (NEW)
- `IAsyncDisposable` support (NEW)

#### Rate Limiting Algorithm Change

**Potential Behavior Change:** The underlying rate limiting algorithm has changed from a custom semaphore-based implementation to the .NET built-in `SlidingWindowRateLimiter`.

The `SlidingWindowRateLimiter` uses a sliding window algorithm that divides the time window into segments. This may result in slightly different timing behavior compared to the original `RateGate`:

- The original `RateGate` released permits exactly when the time unit elapsed from when each permit was acquired
- The new `SlidingWindowRateLimiter` releases permits when window segments slide out

In most practical scenarios, this difference should not be noticeable, but extremely precise rate limiting requirements may observe different timing characteristics.

### `RateLimit` Class Changes

**New Method:** `RateLimit.CreateRateLimiter()` - Factory method to create an `ApiRateLimiter` instance:

```csharp
var rateLimit = RateLimit.Build(10, TimeSpan.FromSeconds(1));
var limiter = rateLimit.CreateRateLimiter();
```

### `EnumerableExtensions` Changes

The `LimitRate<T>` extension method has been updated to use `ApiRateLimiter` internally. The public API remains unchanged:

```csharp
// This continues to work the same way
var rateLimitedItems = items.LimitRate(RateLimit.Build(10, TimeSpan.FromSeconds(1)));
```

**New Overload:** A new overload accepting `ApiRateLimiter` is available:

```csharp
using var limiter = new ApiRateLimiter(RateLimit.Build(10, TimeSpan.FromSeconds(1)));
var rateLimitedItems = items.LimitRate(limiter);
```

**Deprecated Overload:** The overload accepting `RateGate` is now marked as obsolete:

```csharp
// This still works but generates a warning
#pragma warning disable CS0618
var rateGate = new RateGate(RateLimit.Build(10, TimeSpan.FromSeconds(1)));
var rateLimitedItems = items.LimitRate(rateGate);
#pragma warning restore CS0618
```

## Benefits of the Migration

1. **Built-in Support:** Uses Microsoft's official rate limiting APIs, ensuring long-term support and maintenance
2. **Better Performance:** The `System.Threading.RateLimiting` implementation is highly optimized
3. **Modern Async Support:** Native async/await support with proper cancellation token handling
4. **Standardization:** Aligns with the .NET ecosystem's standard approach to rate limiting
5. **Rich Configuration:** Access to multiple rate limiting algorithms (Token Bucket, Fixed Window, Sliding Window, Concurrency) through the `System.Threading.RateLimiting` namespace
6. **Statistics:** Ability to query current permit availability via `GetAvailablePermits()`

## Minimum Requirements

- .NET 8.0 or later (for the main library)
- `System.Threading.RateLimiting` NuGet package version 9.0.0 or later
