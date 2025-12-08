using Polly;

namespace Agile.API.Clients.Infrastructure
{
    /// <summary>
    /// Provides retry policies for HTTP request resilience.
    /// Implement this interface to customize retry behavior or use <see cref="RetryPolicyProvider"/> for default behavior.
    /// </summary>
    public interface IRetryPolicyProvider
    {
        /// <summary>
        /// Returns the default retry policy for transient HTTP errors and
        /// non-success responses (excluding 429 and 403).
        /// </summary>
        /// <returns>The default Polly async retry policy for HTTP requests.</returns>
        IAsyncPolicy<HttpResponseMessage> GetDefaultRetryPolicy();

        /// <summary>
        /// Returns a retry policy for HTTP 429 Too Many Requests responses.
        /// </summary>
        /// <returns>The Polly async retry policy for 429 responses.</returns>
        IAsyncPolicy<HttpResponseMessage> GetTooManyRequestsPolicy();

        /// <summary>
        /// Returns a policy for handling all 4xx responses except 429 (Too Many Requests).
        /// </summary>
        /// <returns>The Polly async policy for 4xx responses (excluding 429).</returns>
        IAsyncPolicy<HttpResponseMessage> GetClientErrorPolicy();

        /// <summary>
        /// Wraps all retry policies into a single composite policy for HTTP requests.
        /// </summary>
        /// <returns>A composite policy combining all retry strategies.</returns>
        IAsyncPolicy<HttpResponseMessage> GetRetryPolicies();
    }
}
