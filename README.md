# Agile.API.Clients

A .NET 8.0+ library for robust, testable, and extensible interaction with Azure DevOps APIs, focusing on release management and pipeline automation.

## Features
- **Abstractions for API calls**: Interfaces like `IApiMethod<TResponse>` for dependency injection and testing.
- **Call Handling**: Unified result types, serialization, and error handling.
- **Rate Limiting**: Built-in support for API rate limiting and retry logic.
- **Helpers**: Utilities for media types, server time, and more.
- **Extensible**: Designed for easy extension and integration into your own services.

## Getting Started

### Installation
Add a reference to `Agile.API.Clients` in your .NET project:

```shell
# currently there is not a nuget package
# Using dotnet CLI
# dotnet add package Agile.API.Clients
```

### Example Usage

```csharp
using Agile.API.Clients;
using Agile.API.Clients.CallHandling;

public class MyApi
{
    private readonly IApiMethod<TResponse> _apiMethod;
    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="configuration">config</param>
    /// <param name="postMethod">use to pass in mocks for testing</param>
    public MyApi(IApiMethod<TResponse>? apiMethod = null)
    {
        _apiMethod = apiMethod ?? PublicGet<TResponse>(MethodPriority.Normal, MediaTypes.JSON);
    }

    public async Task<CallResult<TResponse>> GetDataAsync(string endpoint)
    {
        return await _apiMethod.Call<object>(endpoint, null);
    }
}
```

## API Reference

### `IApiMethod<TResponse>`

```csharp
Task<CallResult<TResponse>> Call<T>(string path, T payload, string querystring = "", CancellationToken cancellationToken = default);
```
- **path**: API endpoint
- **payload**: JSON payload
- **querystring**: Optional query string
- **cancellationToken**: For async cancellation

See XML comments in code for full documentation.

## Best Practices
- Use dependency injection for all API abstractions.
- Handle `CallResult<T>` for error and success cases.
- Respect rate limits and use built-in retry logic.
- Never commit secrets or access tokens.

## Contributing
- Follow the code style and structure.
- Add or update tests for new features.
- Document public APIs with XML comments.
- Submit pull requests with clear descriptions.

## License
MIT
