using Polly;
using System.Net;
using Polly.Extensions.Http;

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
            return HttpPolicyExtensions
                .HandleTransientHttpError() // 5xx and network errors
                .OrResult(r =>
                    r.StatusCode != HttpStatusCode.TooManyRequests
                    && r.StatusCode != HttpStatusCode.Forbidden
                    && r.StatusCode != HttpStatusCode.NotFound
                    && !r.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    onRetry: (outcome, timespan, attempt, context) =>
                    {
                        Console.WriteLine($"POLLY default retry {attempt} after {timespan.TotalSeconds} seconds due to {outcome?.Result?.StatusCode}");
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
            return Policy
                .HandleResult<HttpResponseMessage>(r => r.StatusCode == HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(
                    retryCount: 4,
                    sleepDurationProvider: _ => TimeSpan.FromSeconds(14),
                    onRetry: (outcome, timespan, attempt, context) =>
                    {
                        Console.WriteLine($"POLLY 429 Retry {attempt} after {timespan.TotalSeconds} seconds");
                    });
        }

        /// <summary>
        /// Returns a retry policy for HTTP 403 Forbidden responses.
        /// Does not retry, as forbidden errors are unlikely to be resolved by retrying.
        /// Reason: Retrying 403 is not useful; access is denied and will not change without intervention.
        /// </summary>
        /// <returns>The Polly async retry policy for 403 responses.</returns>
        public static IAsyncPolicy<HttpResponseMessage> GetForbiddenPolicy()
        {
            return Policy
                .HandleResult<HttpResponseMessage>(r => r.StatusCode == HttpStatusCode.Forbidden)
                .WaitAndRetryAsync(
                    retryCount: 0,
                    sleepDurationProvider: _ => TimeSpan.Zero,
                    onRetry: (outcome, timespan, attempt, context) =>
                    {
                        Console.WriteLine($"POLLY 403 Retry - should not be happening!");
                    });
        }

        /// <summary>
        /// Returns a retry policy for HTTP 404 Not Found responses.
        /// Does not retry, as the resource is not found and retrying will not change the result.
        /// Reason: Retrying 404 is not useful; the resource does not exist or is unavailable.
        /// </summary>
        /// <returns>The Polly async retry policy for 404 responses.</returns>
        public static IAsyncPolicy<HttpResponseMessage> GetNotFoundPolicy()
        {
            return Policy
                .HandleResult<HttpResponseMessage>(r => r.StatusCode == HttpStatusCode.NotFound)
                .WaitAndRetryAsync(
                    retryCount: 0,
                    sleepDurationProvider: _ => TimeSpan.Zero,
                    onRetry: (outcome, timespan, attempt, context) =>
                    {
                        Console.WriteLine($"POLLY 404 Retry - should not be happening!");
                    });
        }

        /// <summary>
        /// Wraps all retry policies into a single composite policy for HTTP requests.
        /// Combines default, TooManyRequests, Forbidden, and NotFound policies for comprehensive error handling.
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicies()
        {
            return Policy.WrapAsync(GetDefaultRetryPolicy(),
                GetTooManyRequestsPolicy(), 
                GetForbiddenPolicy(),
                GetNotFoundPolicy());
        }

    }
}
