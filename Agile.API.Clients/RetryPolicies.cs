using Polly;
using System.Net;
using Polly.Extensions.Http;

namespace Agile.API.Clients
{
    public static class RetryPolicies
    {
        // Retry policy for general transient errors
        public static IAsyncPolicy<HttpResponseMessage> GetDefaultRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError() // 5xx and network errors
                .OrResult(r =>
                    r.StatusCode != HttpStatusCode.TooManyRequests
                    && r.StatusCode != HttpStatusCode.Forbidden
                    && !r.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    onRetry: (outcome, timespan, attempt, context) =>
                    {
                        Console.WriteLine($"POLLY default retry {attempt} after {timespan.TotalSeconds} seconds due to {outcome?.Result?.StatusCode}");
                    });
        }

        // Retry policy for 429 Too Many Requests
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
        // Retry policy for 403 Forbidden
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

        // Wrap the two policies
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicies()
        {
            return Policy.WrapAsync(GetTooManyRequestsPolicy(), GetForbiddenPolicy(), GetDefaultRetryPolicy());
        }

    }
}
