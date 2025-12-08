using Polly;
using System.Net;
using Polly.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agile.API.Clients
{
    public static class RetryPolicies
    {
        /// <summary>
        /// Returns the default retry policy for transient HTTP errors and
        /// non-success responses (excluding 429 and 403).
        /// Retries on 5xx, network errors, and other non-success status codes except TooManyRequests (429) and Forbidden (403).
        /// Reason: Retrying transient errors and most non-success codes can resolve temporary issues, but 429 and 403 require special handling.
        /// </summary>
        /// <returns>The default Polly async retry policy for HTTP requests.</returns>
        public static IAsyncPolicy<HttpResponseMessage> GetDefaultRetryPolicy()
        {
            return GetDefaultRetryPolicy(NullLogger.Instance);
        }

        /// <summary>
        /// Returns the default retry policy for transient HTTP errors and
        /// non-success responses (excluding 429 and 403) with logging support.
        /// </summary>
        /// <param name="logger">The logger to use for retry logging.</param>
        /// <returns>The default Polly async retry policy for HTTP requests.</returns>
        public static IAsyncPolicy<HttpResponseMessage> GetDefaultRetryPolicy(ILogger logger)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError() // 5xx and network errors
                .OrResult(r =>
                    !r.IsSuccessStatusCode &&
                    r.StatusCode != HttpStatusCode.TooManyRequests &&
                    r.StatusCode != HttpStatusCode.Forbidden &&
                    r.StatusCode != HttpStatusCode.BadRequest &&
                    r.StatusCode != HttpStatusCode.NotFound)

                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    onRetry: (outcome, timespan, attempt, context) =>
                    {
                        logger.LogWarning(
                            "HTTP retry {Attempt} after {DelaySeconds}s due to {StatusCode}",
                            attempt,
                            timespan.TotalSeconds,
                            outcome?.Result?.StatusCode);
                    });
        }

        /// <summary>
        /// Returns a retry policy for HTTP 429 Too Many Requests responses.
        /// Retries with a fixed delay to allow time for rate limits to reset.
        /// Reason: Retrying after a delay may succeed once the rate limit window has passed.
        /// </summary>
        /// <returns>The Polly async retry policy for 429 responses.</returns>
        public static IAsyncPolicy<HttpResponseMessage> GetTooManyRequestsPolicy()
        {
            return GetTooManyRequestsPolicy(NullLogger.Instance);
        }

        /// <summary>
        /// Returns a retry policy for HTTP 429 Too Many Requests responses with logging support.
        /// </summary>
        /// <param name="logger">The logger to use for retry logging.</param>
        /// <returns>The Polly async retry policy for 429 responses.</returns>
        public static IAsyncPolicy<HttpResponseMessage> GetTooManyRequestsPolicy(ILogger logger)
        {
            return Policy
                .HandleResult<HttpResponseMessage>(r => r.StatusCode == HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(
                    retryCount: 4,
                    sleepDurationProvider: _ => TimeSpan.FromSeconds(14),
                    onRetry: (outcome, timespan, attempt, context) =>
                    {
                        logger.LogWarning(
                            "Rate limited (429). Retry {Attempt} after {DelaySeconds}s",
                            attempt,
                            timespan.TotalSeconds);
                    });
        }


        /// <summary>
        /// Returns a policy for handling all 4xx responses except 429 (Too Many Requests).
        /// Does not retry, as client errors are unlikely to be resolved by retrying.
        /// Reason: Retrying 4xx (except 429) is not useful; client errors require intervention.
        /// </summary>
        /// <returns>The Polly async policy for 4xx responses (excluding 429).</returns>
        public static IAsyncPolicy<HttpResponseMessage> GetClientErrorPolicy()
        {
            return GetClientErrorPolicy(NullLogger.Instance);
        }

        /// <summary>
        /// Returns a policy for handling all 4xx responses except 429 (Too Many Requests) with logging support.
        /// </summary>
        /// <param name="logger">The logger to use for retry logging.</param>
        /// <returns>The Polly async policy for 4xx responses (excluding 429).</returns>
        public static IAsyncPolicy<HttpResponseMessage> GetClientErrorPolicy(ILogger logger)
        {
            return Policy
                .HandleResult<HttpResponseMessage>(r =>
                    (int)r.StatusCode >= 400 && (int)r.StatusCode < 415)
                .WaitAndRetryAsync(
                    retryCount: 0,
                    sleepDurationProvider: _ => TimeSpan.Zero,
                    onRetry: (outcome, timespan, attempt, context) =>
                    {
                        // This should not happen since retryCount is 0, but log if it does
                        logger.LogError(
                            "Unexpected retry for client error. Status: {StatusCode}",
                            outcome?.Result?.StatusCode);
                    });
        }

        /// <summary>
        /// Wraps all retry policies into a single composite policy for HTTP requests.
        /// Combines default, TooManyRequests, Forbidden, and NotFound policies for comprehensive error handling.
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicies()
        {
            return GetRetryPolicies(NullLogger.Instance);
        }

        /// <summary>
        /// Wraps all retry policies into a single composite policy for HTTP requests with logging support.
        /// </summary>
        /// <param name="logger">The logger to use for retry logging.</param>
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicies(ILogger logger)
        {
            return Policy.WrapAsync(
                GetDefaultRetryPolicy(logger),
                GetTooManyRequestsPolicy(logger),
                GetClientErrorPolicy(logger)
            );
        }

    }
}
