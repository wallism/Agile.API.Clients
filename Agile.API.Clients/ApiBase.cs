using System.Diagnostics;
using System.Net.Http.Headers;
using Agile.API.Clients.CallHandling;
using Agile.API.Clients.Helpers;
using Agile.API.Clients.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;

namespace Agile.API.Clients
{
    /// <summary>
    /// Base class for building API clients with built-in rate limiting, retry policies, and error handling.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class follows the Single Responsibility Principle by delegating HTTP client management
    /// to <see cref="IHttpClientProvider"/> and rate limiting to <see cref="IRateLimitHandler"/>.
    /// </para>
    /// <para>
    /// Derived classes should implement <see cref="BaseUrl"/> and <see cref="ApiId"/> properties,
    /// and optionally override <see cref="SetPrivateRequestProperties"/> for authentication.
    /// </para>
    /// </remarks>
    public abstract class ApiBase : IDisposable, IAsyncDisposable
    {
        private readonly IHttpClientProvider _httpClientProvider;
        private readonly IRateLimitHandler _rateLimitHandler;
        private readonly ILogger _logger;
        private bool _isDisposed;

        /// <summary>
        /// Gets the configuration source.
        /// </summary>
        protected IConfiguration Configuration { get; }

        /// <summary>
        /// Gets the logger instance for this API client.
        /// </summary>
        protected ILogger Logger => _logger;

        /// <summary>
        /// Initializes a new instance using IHttpClientFactory directly (legacy constructor).
        /// Creates default implementations of collaborators internally.
        /// </summary>
        /// <param name="configuration">Configuration for rate limit settings.</param>
        /// <param name="httpClientFactory">Factory for creating HttpClient instances.</param>
        /// <param name="logger">Optional logger instance. If null, a NullLogger is used.</param>
        protected ApiBase(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger? logger = null)
            : this(
                configuration,
                new HttpClientProvider(httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory))),
                null, // Rate handler created in chained constructor using ApiId
                logger)
        {
        }

        /// <summary>
        /// Initializes a new instance with explicit collaborators for better testability and SRP compliance.
        /// </summary>
        /// <param name="configuration">Configuration for API settings.</param>
        /// <param name="httpClientProvider">Provider for HttpClient instances.</param>
        /// <param name="rateLimitHandler">Handler for rate limiting (null to create from configuration).</param>
        /// <param name="logger">Optional logger instance. If null, a NullLogger is used.</param>
        protected ApiBase(
            IConfiguration configuration,
            IHttpClientProvider httpClientProvider,
            IRateLimitHandler? rateLimitHandler,
            ILogger? logger = null)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClientProvider = httpClientProvider ?? throw new ArgumentNullException(nameof(httpClientProvider));
            
            // If no rate handler provided, create one from configuration
            // Note: This requires ApiId to be available, which it is since it's abstract
            _rateLimitHandler = rateLimitHandler ?? new RateLimitHandler(configuration, ApiId);
            
            // Use NullLogger if no logger provided - follows Null Object pattern
            // This ensures logging calls are always safe without null checks
            _logger = logger ?? NullLogger.Instance;
            
            // Log rate limit configuration for diagnostics
            if (_rateLimitHandler.IsEnabled && _rateLimitHandler is RateLimitHandler handler)
            {
                _logger.LogInformation(
                    "{ApiId} RateLimit configured: {Occurrences} occurrences per {Seconds} seconds",
                    ApiId,
                    handler.Occurrences,
                    handler.TimeUnitSeconds);
            }
        }

        /// <summary>
        /// Sets the authorization header on the HttpClient.
        /// </summary>
        /// <param name="header">The authentication header value.</param>
        protected void SetAuthorizationHeader(AuthenticationHeaderValue header)
        {
            HttpClient.DefaultRequestHeaders.Authorization = header;
        }

        /// <summary>
        /// Gets whether rate limiting is enabled for this API.
        /// </summary>
        public bool HasRateLimit => _rateLimitHandler.IsEnabled;

        /// <summary>
        /// Gets the HttpClient instance for this API.
        /// Thread-safe: uses IHttpClientProvider for proper synchronization.
        /// </summary>
        private HttpClient HttpClient => _httpClientProvider.GetClient(HttpClientName);

        /// <summary>
        /// Gets the base URL for this API.
        /// </summary>
        protected abstract string BaseUrl { get; }

        /// <summary>
        /// Identifies which API this is (useful for logging and configuration lookup).
        /// </summary>
        public abstract string ApiId { get; }

        /// <summary>
        /// Gets or sets the named HttpClient to use from the factory.
        /// </summary>
        protected virtual string HttpClientName { get; set; } = DefaultHttpClientName;

        /// <summary>
        /// The default HttpClient name used when none is specified.
        /// </summary>
        public const string DefaultHttpClientName = "DefaultHttpClient";

        public ApiMethod<T> PublicGet<T>(MethodPriority priority) where T : class
        {
            return PublicGet<T>(priority, MediaTypes.JSON);
        }

        public ApiMethod<T> PublicGet<T>(MethodPriority priority, MediaTypeHeaderValue contentType) where T : class
        {
            return new PublicMethod<T>(this, HttpMethod.Get, priority, contentType);
        }


        public ApiMethod<T> PrivateGet<T>(MethodPriority priority) where T : class
        {
            return PrivateGet<T>(priority, MediaTypes.JSON);
        }

        public ApiMethod<T> PrivateGet<T>(MethodPriority priority, MediaTypeHeaderValue contentType) where T : class
        {
            return new PrivateMethod<T>(this, HttpMethod.Get, priority, contentType);
        }


        public ApiMethod<T> PrivatePost<T>(MethodPriority priority) where T : class
        {
            return PrivatePost<T>(priority, MediaTypes.JSON);
        }

        public ApiMethod<T> PrivatePost<T>(MethodPriority priority, MediaTypeHeaderValue contentType) where T : class
        {
            return new PrivateMethod<T>(this, HttpMethod.Post, priority, contentType);
        }

        public ApiMethod<T> PrivatePut<T>(MethodPriority priority) where T : class
        {
            return PrivatePut<T>(priority, MediaTypes.JSON);
        }

        public ApiMethod<T> PrivatePut<T>(MethodPriority priority, MediaTypeHeaderValue contentType) where T : class
        {
            return new PrivateMethod<T>(this, HttpMethod.Put, priority, contentType);
        }

        public ApiMethod<T> PrivateDelete<T>(MethodPriority priority) where T : class
        {
            return new PrivateMethod<T>(this, HttpMethod.Delete, priority, MediaTypes.JSON);
        }

        protected virtual long GetNonce()
        {
            return ServerTime.UnixTimeStampUtc();
        }

        protected virtual string GetPublicRequestUri(string path, string querystring = "")
        {
            // by default public is same as private, separate overload provided because for some API's they are indeed different.
            return GetPrivateRequestUri(path, querystring);
        }

        protected virtual string GetPrivateRequestUri(string path, string querystring = "")
        {
            var url = path.StartsWith(BaseUrl) // allow full url to be passed (useful for pagination)
                ? path
                : $"{BaseUrl}/{path}";
            return string.IsNullOrWhiteSpace(querystring)
                ? url
                : $"{url}?{querystring}";
        }


        /// <summary>
        /// Sets properties on a public HTTP request before sending.
        /// </summary>
        /// <param name="request">The request to configure.</param>
        /// <param name="method">The API method path.</param>
        /// <param name="rawPayload">Optional payload for the request.</param>
        /// <param name="propsWithNonce">Optional properties with nonce.</param>
        protected virtual Task SetPublicRequestProperties(HttpRequestMessage request, string method, object? rawPayload = null, string propsWithNonce = "")
        {
            if (request.RequestUri is not null)
            {
                request.Headers.Host = request.RequestUri.Host;
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sets properties on a private (authenticated) HTTP request before sending.
        /// </summary>
        /// <param name="request">The request to configure.</param>
        /// <param name="method">The API method path.</param>
        /// <param name="rawPayload">Optional payload for the request.</param>
        /// <param name="propsWithNonce">Optional properties with nonce.</param>
        /// <exception cref="NotImplementedException">Thrown when not overridden in derived class.</exception>
        protected virtual Task SetPrivateRequestProperties(HttpRequestMessage request, string method, object? rawPayload = null, string propsWithNonce = "")
        {
            throw new NotImplementedException("Required if calling private methods on the API");
        }


        /// <summary>
        /// Asynchronously enforces rate limiting before an API call.
        /// </summary>
        /// <typeparam name="T">The response type.</typeparam>
        /// <param name="method">The API method being called.</param>
        /// <param name="cancellationToken">Token to cancel the wait operation.</param>
        private async ValueTask PassThroughRateGateAsync<T>(ApiMethod<T> method, CancellationToken cancellationToken) where T : class
        {
            if (method.IsHighPriority)
            {
                _rateLimitHandler.NotifyPriorityCall();
            }
            else
            {
                await _rateLimitHandler.WaitToProceedAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Releases unmanaged resources held by this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged resources held by this instance.
        /// </summary>
        /// <param name="disposing">Whether this method is being called from Dispose().</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _rateLimitHandler.Dispose();
                }
                _isDisposed = true;
            }
        }

        /// <summary>
        /// Asynchronously releases unmanaged resources held by this instance.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (!_isDisposed)
            {
                await _rateLimitHandler.DisposeAsync().ConfigureAwait(false);
                _isDisposed = true;
            }
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Logs an error when an API call fails.
        /// </summary>
        /// <typeparam name="T">The response type.</typeparam>
        /// <param name="result">The failed call result.</param>
        private void LogError<T>(CallResult<T> result) where T : class
        {
            if (result.Exception is not null)
            {
                _logger.LogError(
                    result.Exception,
                    "API call failed: {ApiId} {StatusCode} {Uri} | {RawText}",
                    ApiId,
                    result.StatusCode,
                    result.AbsoluteUri,
                    TruncateForLogging(result.RawText));
            }
            else
            {
                _logger.LogWarning(
                    "API call unsuccessful: {ApiId} {StatusCode} {Uri} | {RawText}",
                    ApiId,
                    result.StatusCode,
                    result.AbsoluteUri,
                    TruncateForLogging(result.RawText));
            }
        }

        /// <summary>
        /// Truncates raw text for logging to avoid excessive log sizes.
        /// </summary>
        private static string? TruncateForLogging(string? text, int maxLength = 500)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;
            
            return string.Concat(text.AsSpan(0, maxLength), "...[truncated]");
        }


        public class PublicMethod<TResponse> : ApiMethod<TResponse> where TResponse : class
        {
            public PublicMethod(ApiBase api, HttpMethod httpMethod, MethodPriority priority)
                : this(api, httpMethod, priority, MediaTypes.JSON)
            {
            }

            public PublicMethod(ApiBase api, HttpMethod httpMethod, MethodPriority priority, MediaTypeHeaderValue contentType)
                : base(api, httpMethod, priority, contentType)
            {
            }


            //        public static ApiMethod<TResponse> Get(MethodPriority priority, string contentType = ContentTypes.JSON) => new PublicMethod<TResponse>(HttpMethod.Get, priority, contentType);
            //        public static ApiMethod<TResponse> Post(MethodPriority priority, string contentType = ContentTypes.JSON) => new PublicMethod<TResponse>(HttpMethod.Post, priority, contentType);

            protected override async Task<HttpRequestMessage> CreateRequest<T>(string path, string querystring, T payload)
            {
                var uri = Api.GetPublicRequestUri(path, querystring);

                var request = new HttpRequestMessage(HttpMethod, uri);
                await Api.SetPublicRequestProperties(request, path, payload, querystring).ConfigureAwait(false);

                // this adds a content body (POST only)
                AddPayloadToBody(request, payload);
                return request;
            }
        }

        public class PrivateMethod<TResponse> : ApiMethod<TResponse> where TResponse : class
        {
            public PrivateMethod(ApiBase api, HttpMethod httpMethod, MethodPriority priority, MediaTypeHeaderValue contentType)
                : base(api, httpMethod, priority, contentType)
            {
            }

            protected override async Task<HttpRequestMessage> CreateRequest<T>(string path, string querystring, T payload)
            {
                var uri = Api.GetPrivateRequestUri(path, querystring);

                var request = new HttpRequestMessage(HttpMethod, uri);
                await Api.SetPrivateRequestProperties(request, path, payload, querystring).ConfigureAwait(false);

                // this adds a content body (POST only)
                AddPayloadToBody(request, payload);
                return request;
            }
        }

        /// <summary>
        ///     Details about an API method. (only one per method should be instantiated)
        /// </summary>
        /// <remarks>
        ///     Single instance to be created for each method.
        ///     Also allows simplification in the ApiBase, main intent is to help improve readability
        /// </remarks>
        public abstract class ApiMethod<TResponse> where TResponse : class
        {
            public readonly HttpMethod HttpMethod;
            public readonly MediaTypeHeaderValue MethodContentType;


            protected ApiBase Api;

            /// <inheritdoc />
            protected ApiMethod(ApiBase api, HttpMethod httpMethod, MethodPriority priority, MediaTypeHeaderValue contentType)
            {
                Api = api;
                HttpMethod = httpMethod;
                MethodContentType = contentType;
                Priority = priority;
            }


            /// <summary>
            ///     High priority calls will not be stopped by the RateGate, they go straight through.
            ///     (note: The call still gets counted)
            /// </summary>
            public MethodPriority Priority { get; }


            public bool IsHighPriority => Priority == MethodPriority.High;


            public async Task<CallResult<TResponse>> Call<T>(string path,
                T payload,
                string querystring = "",
                CancellationToken cancellationToken = default)
            {
                var request = await CreateRequest(path, querystring, payload).ConfigureAwait(false);
                
                // Async rate limiting - no thread blocking
                await Api.PassThroughRateGateAsync(this, cancellationToken).ConfigureAwait(false);

                HttpResponseMessage? response = null;
                var timer = Stopwatch.StartNew();
                
                try
                {
                    response = await Api.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    timer.Stop();

                    var result = await CallResult<TResponse>.Wrap(request, response, timer.ElapsedMilliseconds, Api._logger).ConfigureAwait(false);

                    if (!result.WasSuccessful)
                        Api.LogError(result);
                    return result;
                }
                catch (Exception ex)
                {
                    timer.Stop();
                    var result = CallResult<TResponse>.BuildException(ex, request, timer.ElapsedMilliseconds);
                    Api.LogError(result);
                    return result;
                }
                finally
                {
                    response?.Dispose();
                }
            }


            protected abstract Task<HttpRequestMessage> CreateRequest<T>(string path, string querystring, T payload);

            /// <summary>
            ///     Add the given payload to the body of the request
            /// </summary>
            protected void AddPayloadToBody<T>(HttpRequestMessage request, T payload)
            {
                if (request.Method == HttpMethod.Get || payload == null)
                    return;

                if (Equals(MethodContentType, MediaTypes.FormUrlEncoded))
                {
                    var formData = (IEnumerable<KeyValuePair<string, string>>)payload;
                    request.Content = new FormUrlEncodedContent(formData);
                }
                else if(payload is string stringPayload)
                {
                    request.Content = new StringContent(stringPayload);
                }
                else
                {
                    var serializedPayload = JsonConvert.SerializeObject(payload);
                    request.Content = new StringContent(serializedPayload);
                }

                request.Content.Headers.ContentType = MethodContentType;
            }
        }
    }
}