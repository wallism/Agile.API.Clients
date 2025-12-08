namespace Agile.API.Clients.Infrastructure
{
    /// <summary>
    /// Handles rate limiting for API calls in an async-first manner.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This abstraction separates rate limiting concerns from the API base class,
    /// adhering to the Single Responsibility Principle.
    /// </para>
    /// <para>
    /// Implementations should support both regular and high-priority calls.
    /// High-priority calls should still be counted against the rate limit
    /// but should not block execution.
    /// </para>
    /// </remarks>
    public interface IRateLimitHandler : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Gets whether rate limiting is enabled.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Asynchronously waits until the caller is allowed to proceed based on rate limits.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the wait operation.</param>
        /// <returns>A task that completes when the caller may proceed.</returns>
        ValueTask WaitToProceedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Notifies the rate limiter that a high-priority call is being made.
        /// The call proceeds immediately but is still counted against the rate limit.
        /// </summary>
        void NotifyPriorityCall();
    }
}
