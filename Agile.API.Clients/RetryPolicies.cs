using Agile.API.Clients.Infrastructure;
using Microsoft.Extensions.Logging;
using Polly;

namespace Agile.API.Clients
{
    /// <summary>
    /// Static convenience methods for retry policies.
    /// For DI scenarios, use <see cref="IRetryPolicyProvider"/> and <see cref="RetryPolicyProvider"/> instead.
    /// </summary>
    /// <remarks>
    /// These static methods delegate to a default <see cref="RetryPolicyProvider"/> instance
    /// and are provided for backward compatibility and simple usage scenarios.
    /// For production applications, prefer injecting <see cref="IRetryPolicyProvider"/>.
    /// </remarks>
    public static class RetryPolicies
    {
        private static readonly RetryPolicyProvider DefaultProvider = new();

        /// <summary>
        /// Returns the default retry policy for transient HTTP errors and
        /// non-success responses (excluding 429 and 403).
        /// Retries on 5xx, network errors, and other non-success status codes except TooManyRequests (429) and Forbidden (403).
        /// Reason: Retrying transient errors and most non-success codes can resolve temporary issues, but 429 and 403 require special handling.
        /// </summary>
        /// <returns>The default Polly async retry policy for HTTP requests.</returns>
        public static IAsyncPolicy<HttpResponseMessage> GetDefaultRetryPolicy()
            => DefaultProvider.GetDefaultRetryPolicy();

        /// <summary>
        /// Returns the default retry policy for transient HTTP errors and
        /// non-success responses (excluding 429 and 403) with logging support.
        /// </summary>
        /// <param name="logger">The logger to use for retry logging.</param>
        /// <returns>The default Polly async retry policy for HTTP requests.</returns>
        public static IAsyncPolicy<HttpResponseMessage> GetDefaultRetryPolicy(ILogger logger)
            => new RetryPolicyProvider(logger).GetDefaultRetryPolicy();

        /// <summary>
        /// Returns a retry policy for HTTP 429 Too Many Requests responses.
        /// Retries with a fixed delay to allow time for rate limits to reset.
        /// Reason: Retrying after a delay may succeed once the rate limit window has passed.
        /// </summary>
        /// <returns>The Polly async retry policy for 429 responses.</returns>
        public static IAsyncPolicy<HttpResponseMessage> GetTooManyRequestsPolicy()
            => DefaultProvider.GetTooManyRequestsPolicy();

        /// <summary>
        /// Returns a retry policy for HTTP 429 Too Many Requests responses with logging support.
        /// </summary>
        /// <param name="logger">The logger to use for retry logging.</param>
        /// <returns>The Polly async retry policy for 429 responses.</returns>
        public static IAsyncPolicy<HttpResponseMessage> GetTooManyRequestsPolicy(ILogger logger)
            => new RetryPolicyProvider(logger).GetTooManyRequestsPolicy();

        /// <summary>
        /// Returns a policy for handling all 4xx responses except 429 (Too Many Requests).
        /// Does not retry, as client errors are unlikely to be resolved by retrying.
        /// Reason: Retrying 4xx (except 429) is not useful; client errors require intervention.
        /// </summary>
        /// <returns>The Polly async policy for 4xx responses (excluding 429).</returns>
        public static IAsyncPolicy<HttpResponseMessage> GetClientErrorPolicy()
            => DefaultProvider.GetClientErrorPolicy();

        /// <summary>
        /// Returns a policy for handling all 4xx responses except 429 (Too Many Requests) with logging support.
        /// </summary>
        /// <param name="logger">The logger to use for retry logging.</param>
        /// <returns>The Polly async policy for 4xx responses (excluding 429).</returns>
        public static IAsyncPolicy<HttpResponseMessage> GetClientErrorPolicy(ILogger logger)
            => new RetryPolicyProvider(logger).GetClientErrorPolicy();

        /// <summary>
        /// Wraps all retry policies into a single composite policy for HTTP requests.
        /// Combines default, TooManyRequests, Forbidden, and NotFound policies for comprehensive error handling.
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicies()
            => DefaultProvider.GetRetryPolicies();

        /// <summary>
        /// Wraps all retry policies into a single composite policy for HTTP requests with logging support.
        /// </summary>
        /// <param name="logger">The logger to use for retry logging.</param>
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicies(ILogger logger)
            => new RetryPolicyProvider(logger).GetRetryPolicies();
    }
}
