# Copilot Instructions for Agile.API.Clients

This document provides guidance for GitHub Copilot when working with this codebase.

## Project Overview

**Agile.API.Clients** is a .NET 10.0 library providing a foundation for building robust, testable API clients with built-in rate limiting, retry policies, and unified error handling.

## Technology Stack

- **Framework**: .NET 10.0 (C# 14)
- **Key Dependencies**:
  - `Microsoft.Extensions.Http` - HttpClientFactory support
  - `Microsoft.Extensions.Http.Polly` / `Microsoft.Extensions.Http.Resilience` - Retry policies
  - `Polly` - Resilience and transient fault handling
  - `Newtonsoft.Json` / `System.Text.Json` - JSON serialization
- **Testing**: xUnit with mocking support

## Code Style & Conventions

### General C# Guidelines

- Use **nullable reference types** (`<Nullable>enable</Nullable>`)
- Use **implicit usings** (`<ImplicitUsings>enable</ImplicitUsings>`)
- Follow **SOLID principles** throughout the codebase
- Prefer **async/await** for all I/O operations
- Use **CancellationToken** for all async methods

### Naming Conventions

- Use `PascalCase` for public members, types, and namespaces
- Use `camelCase` for local variables and parameters
- Prefix private fields with underscore: `_privateField`
- Suffix async methods with `Async`: `GetDataAsync()`

### Class Design

- Inherit from `ApiBase` for new API clients
- Implement `IDisposable` and `IAsyncDisposable` when managing resources
- Use dependency injection for `IConfiguration` and `IHttpClientFactory`
- Keep classes focused on a single responsibility

## Key Patterns

### Creating API Clients

```csharp
public class MyApi : ApiBase
{
    public MyApi(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        : base(configuration, httpClientFactory)
    {
    }

    protected override string BaseUrl => "https://api.example.com";
    public override string ApiId => "MyApi";

    public async Task<CallResult<TResponse>> GetResourceAsync(
        string id, 
        CancellationToken cancellationToken = default)
    {
        var method = PublicGet<TResponse>(MethodPriority.Normal);
        return await method.Call($"resources/{id}", null, cancellationToken: cancellationToken);
    }
}
```

### Handling API Results

Always use `CallResult<T>` pattern:

```csharp
var result = await api.GetResourceAsync(id);

if (result.WasSuccessful)
{
    // Use result.Value
}
else
{
    // Handle result.Exception, result.StatusCode, result.RawText
}
```

### Rate Limiting Configuration

Configure via `IConfiguration`:

```json
{
  "APIS": {
    "ApiId": {
      "RateLimit": {
        "Occurrences": "10",
        "Seconds": "1"
      }
    }
  }
}
```

## File Organization

```
Agile.API.Clients/
├── ApiBase.cs              # Base class - extend for new APIs
├── MethodPriority.cs       # Rate limit priorities
├── RetryPolicies.cs        # Polly configurations
├── CallHandling/           # Result types and serialization
├── Helpers/                # Utilities (MediaTypes, ServerTime)
└── RateLimiting/           # Rate limiting infrastructure

Agile.API.Clients.Tests/
├── *Tests.cs               # Unit tests (xUnit)
└── Mocks/                  # Mock implementations for testing
```

## Testing Guidelines

- Write unit tests for all public methods
- Use the `Mocks/` folder for test doubles
- Test both success and failure scenarios for `CallResult<T>`
- Test rate limiting behavior with `ApiRateLimiterTests`

### Test Naming Convention

```csharp
[Fact]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange, Act, Assert
}
```

## Common Tasks

### Adding a New API Client

1. Create a new class inheriting from `ApiBase`
2. Implement `BaseUrl` and `ApiId` properties
3. Override `SetPrivateRequestProperties` for authentication
4. Create methods using `PublicGet`, `PrivateGet`, `PrivatePost`, etc.
5. Add corresponding unit tests

### Adding Authentication

Override `SetPrivateRequestProperties` in your API class:

```csharp
protected override async Task SetPrivateRequestProperties(
    HttpRequestMessage request, 
    string method, 
    object? rawPayload = null, 
    string propsWithNonce = "")
{
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
}
```

### Customizing Rate Limits

Either configure via `IConfiguration` or override in constructor:

```csharp
public MyApi(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    : base(configuration, httpClientFactory)
{
    RateGateOccurrences = "5";
    RateGateSeconds = "1";
}
```

## Do's and Don'ts

### Do

- ✅ Use `CallResult<T>` for all API responses
- ✅ Pass `CancellationToken` through all async call chains
- ✅ Use `IHttpClientFactory` (never create `HttpClient` directly)
- ✅ Dispose API clients properly (implement `IAsyncDisposable`)
- ✅ Add XML documentation comments to public APIs
- ✅ Write unit tests for new functionality

### Don't

- ❌ Commit secrets or API keys to source control
- ❌ Ignore `CallResult.WasSuccessful` checks
- ❌ Create `HttpClient` instances directly
- ❌ Block on async code (no `.Result` or `.Wait()`)
- ❌ Swallow exceptions without logging/handling

## Helpful Context

- The `ApiBase` class manages `HttpClient` lifecycle via `IHttpClientFactory`
- Rate limiting is automatic; configure via `APIS:{ApiId}:RateLimit` settings
- `MethodPriority.High` bypasses rate limiting for critical calls
- `CallResult<T>` captures the full response including raw text for debugging
