namespace Agile.API.Clients.Infrastructure
{
    /// <summary>
    /// Provides thread-safe access to HttpClient instances.
    /// </summary>
    /// <remarks>
    /// This abstraction separates HTTP client management concerns from the API base class,
    /// adhering to the Single Responsibility Principle. The default implementation uses
    /// IHttpClientFactory for proper connection pooling and lifetime management.
    /// </remarks>
    public interface IHttpClientProvider
    {
        /// <summary>
        /// Gets an HttpClient instance configured for the specified API.
        /// </summary>
        /// <param name="clientName">The named client to retrieve from the factory.</param>
        /// <returns>A thread-safe HttpClient instance.</returns>
        HttpClient GetClient(string clientName);
    }
}
