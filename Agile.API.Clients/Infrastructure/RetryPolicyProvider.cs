using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.Extensions.Http;

namespace Agile.API.Clients.Infrastructure
{
    /// <summary>
    /// Default implementation of <see cref="IRetryPolicyProvider"/> that provides
    /// comprehensive HTTP retry policies with configurable logging.
    /// </summary>
    /// <remarks>
    /// This class follows the Dependency Injection pattern and can be registered
    /// as a singleton in the service collection for consistent policy behavior.
    /// </remarks>
    public sealed class RetryPolicyProvider : IRetryPolicyProvider
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="RetryPolicyProvider"/> with the specified logger.
        /// </summary>
        /// <param name="logger">The logger to use for retry logging. If null, a null logger is used.</param>
        public RetryPolicyProvider(ILogger<RetryPolicyProvider>? logger = null)
        {
            _logger = logger ?? NullLogger<RetryPolicyProvider>.Instance;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RetryPolicyProvider"/> with a non-generic logger.
        /// </summary>
        /// <param name="logger">The logger to use for retry logging.</param>
        public RetryPolicyProvider(ILogger logger)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        /// <inheritdoc />
        public IAsyncPolicy<HttpResponseMessage> GetDefaultRetryPolicy()
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
                        _logger.LogWarning(
                            "HTTP retry {Attempt} after {DelaySeconds}s due to {StatusCode}",
                            attempt,
                            timespan.TotalSeconds,
                            outcome?.Result?.StatusCode);
                    });
        }

        /// <inheritdoc />
        public IAsyncPolicy<HttpResponseMessage> GetTooManyRequestsPolicy()
        {
            return Policy
                .HandleResult<HttpResponseMessage>(r => r.StatusCode == HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(
                    retryCount: 4,
                    sleepDurationProvider: _ => TimeSpan.FromSeconds(14),
                    onRetry: (outcome, timespan, attempt, context) =>
                    {
                        _logger.LogWarning(
                            "Rate limited (429). Retry {Attempt} after {DelaySeconds}s",
                            attempt,
                            timespan.TotalSeconds);
                    });
        }

        /// <inheritdoc />
        public IAsyncPolicy<HttpResponseMessage> GetClientErrorPolicy()
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
                        _logger.LogError(
                            "Unexpected retry for client error. Status: {StatusCode}",
                            outcome?.Result?.StatusCode);
                    });
        }

        /// <inheritdoc />
        public IAsyncPolicy<HttpResponseMessage> GetRetryPolicies()
        {
            return Policy.WrapAsync(
                GetDefaultRetryPolicy(),
                GetTooManyRequestsPolicy(),
                GetClientErrorPolicy()
            );
        }
    }
}
