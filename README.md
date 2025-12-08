# Agile.API.Clients

A .NET 10.0 library for robust, testable, and extensible API client implementations with built-in rate limiting, retry policies, and unified error handling.

## Features

- **API Base Class**: Abstract `ApiBase` class providing a foundation for building API clients with built-in HTTP client management, rate limiting, and authentication support.
- **Call Handling**: Unified `CallResult<T>` type for consistent success/error handling across all API calls.
- **Rate Limiting**: Built-in `ApiRateLimiter` with configurable limits via `IConfiguration`.
- **Retry Policies**: Integration with Polly for resilient HTTP calls.
- **Helpers**: Utilities for media types (`MediaTypes`), server time (`ServerTime`), and more.
- **Testable**: Designed for dependency injection and easy mocking in unit tests.

## Project Structure

```
Agile.API.Clients/
├── ApiBase.cs              # Abstract base class for API clients
├── MethodPriority.cs       # Priority levels for rate limiting
├── RetryPolicies.cs        # Polly retry policy configurations
├── CallHandling/
│   ├── CallResult.cs       # Unified result wrapper for API calls
│   └── CallSerialization.cs
├── Helpers/
│   ├── MediaTypes.cs       # Common media type constants
│   └── ServerTime.cs       # UTC timestamp utilities
└── RateLimiting/
    ├── ApiRateLimiter.cs   # Rate limiter implementation
    ├── RateGate.cs         # Token bucket rate gate
    └── RateLimit.cs        # Rate limit configuration
```

## Getting Started

### Prerequisites

- .NET 10.0 SDK or later
- Visual Studio 2022+ or VS Code with C# Dev Kit

### Installation

Add a project reference to `Agile.API.Clients` in your .NET project:

```shell
# Currently there is no NuGet package
# Add as a project reference
dotnet add reference ../Agile.API.Clients/Agile.API.Clients.csproj
```

### Example Usage

Create a custom API client by inheriting from `ApiBase`:

```csharp
using Agile.API.Clients;
using Agile.API.Clients.CallHandling;
using Microsoft.Extensions.Configuration;

public class MyApi : ApiBase
{
    public MyApi(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        : base(configuration, httpClientFactory)
    {
    }

    protected override string BaseUrl => "https://api.example.com";
    public override string ApiId => "MyApi";

    public async Task<CallResult<MyResponse>> GetDataAsync(CancellationToken cancellationToken = default)
    {
        var method = PublicGet<MyResponse>(MethodPriority.Normal);
        return await method.Call("v1/data", payload: null, cancellationToken: cancellationToken);
    }

    public async Task<CallResult<MyResponse>> PostDataAsync(MyRequest request, CancellationToken cancellationToken = default)
    {
        var method = PrivatePost<MyResponse>(MethodPriority.Normal);
        return await method.Call("v1/data", payload: request, cancellationToken: cancellationToken);
    }
}
```

### Handling Results

Always check `CallResult<T>` for success before accessing the value:

```csharp
var result = await myApi.GetDataAsync();

if (result.WasSuccessful)
{
    var data = result.Value;
    // Process successful response
}
else
{
    // Handle error
    Console.WriteLine($"Error: {result.StatusCode} - {result.Exception?.Message}");
}
```

### Configuration

Configure rate limits via `IConfiguration` (appsettings.json or user secrets):

```json
{
  "APIS": {
    "MyApi": {
      "RateLimit": {
        "Occurrences": "10",
        "Seconds": "1"
      }
    }
  }
}
```

## API Reference

### `ApiBase`

Abstract base class for API clients. Key members:

| Member | Description |
|--------|-------------|
| `BaseUrl` | Abstract property for the API base URL |
| `ApiId` | Abstract property identifying the API (used in config and logging) |
| `PublicGet<T>()` | Creates a public GET method |
| `PrivateGet<T>()` | Creates an authenticated GET method |
| `PrivatePost<T>()` | Creates an authenticated POST method |
| `PrivateDelete<T>()` | Creates an authenticated DELETE method |

### `CallResult<T>`

Wrapper for API call results:

| Property | Description |
|----------|-------------|
| `WasSuccessful` | `true` if the call succeeded without exceptions |
| `Value` | The deserialized response (when successful) |
| `StatusCode` | HTTP status code |
| `Exception` | Any exception that occurred |
| `RawText` | Raw response text |

See XML comments in code for full documentation.

## Best Practices

- **Use Dependency Injection**: Register `IHttpClientFactory` and `IConfiguration` in your DI container.
- **Handle `CallResult<T>`**: Always check `WasSuccessful` before accessing `Value`.
- **Respect Rate Limits**: Use built-in rate limiting; configure via `IConfiguration`.
- **Override `SetPrivateRequestProperties`**: Implement authentication logic for private endpoints.
- **Never Commit Secrets**: Use user secrets or environment variables for API keys.

## Managing Secrets Locally

To use secrets (such as Azure DevOps access tokens) during local development, follow these best practices:

- **Never commit secrets to source control.**
- Use [user secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) for development:

```shell
# In your project directory
# Initialize user secrets (if not already done)
dotnet user-secrets init

# Add a secret (replace KEY and VALUE)
dotnet user-secrets set "AzureDevOps:AccessToken" "YOUR_TOKEN_HERE"
```

- Access secrets in your code via configuration (e.g., `IConfiguration`).
- For CI/CD or production, use environment variables or a secure vault (e.g., Azure Key Vault).

**Example (accessing a secret in C#):**

```csharp
var accessToken = configuration["AzureDevOps:AccessToken"];
```

See the [Microsoft documentation](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) for more details.


## Contributing
- Follow the code style and structure.
- Add or update tests for new features.
- Document public APIs with XML comments.
- Submit pull requests with clear descriptions.

## License
MIT
