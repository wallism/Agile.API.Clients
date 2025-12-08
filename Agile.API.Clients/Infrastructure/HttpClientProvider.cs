using System.Collections.Concurrent;

namespace Agile.API.Clients.Infrastructure
{
    /// <summary>
    /// Thread-safe implementation of <see cref="IHttpClientProvider"/> using IHttpClientFactory.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class provides thread-safe lazy initialization of HttpClient instances per named client.
    /// Each named client is created once and cached for the lifetime of this provider.
    /// </para>
    /// <para>
    /// Using ConcurrentDictionary with Lazy&lt;T&gt; ensures that even under high concurrency,
    /// only one HttpClient instance is created per named client, preventing resource waste
    /// and ensuring consistent behavior.
    /// </para>
    /// </remarks>
    public sealed class HttpClientProvider : IHttpClientProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ConcurrentDictionary<string, Lazy<HttpClient>> _clients;

        /// <summary>
        /// Initializes a new instance of <see cref="HttpClientProvider"/>.
        /// </summary>
        /// <param name="httpClientFactory">The factory used to create HttpClient instances.</param>
        /// <exception cref="ArgumentNullException">Thrown when httpClientFactory is null.</exception>
        public HttpClientProvider(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _clients = new ConcurrentDictionary<string, Lazy<HttpClient>>();
        }

        /// <inheritdoc />
        public HttpClient GetClient(string clientName)
        {
            if (string.IsNullOrWhiteSpace(clientName))
            {
                throw new ArgumentException("Client name cannot be null or whitespace.", nameof(clientName));
            }

            // Thread-safe lazy initialization pattern using ConcurrentDictionary
            // This ensures only one HttpClient is created per named client, even under concurrent access
            var lazyClient = _clients.GetOrAdd(
                clientName,
                name => new Lazy<HttpClient>(
                    () => _httpClientFactory.CreateClient(name),
                    LazyThreadSafetyMode.ExecutionAndPublication));

            return lazyClient.Value;
        }
    }
}
