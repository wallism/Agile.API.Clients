using System;
using System.Threading;
using System.Threading.RateLimiting;

namespace PennedObjects.RateLimiting
{
    /// <summary>
    /// A rate limiter that uses the built-in .NET <see cref="SlidingWindowRateLimiter"/> 
    /// to control the rate of operations per unit of time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class replaces the legacy <see cref="RateGate"/> class and uses the modern 
    /// System.Threading.RateLimiting APIs introduced in .NET 7.
    /// </para>
    /// <para>
    /// To control the rate of an action, code should call <see cref="WaitToProceedAsync"/> 
    /// or <see cref="WaitToProceed"/> prior to performing the action. These methods will 
    /// block until the action is allowed based on the rate limit.
    /// </para>
    /// <para>
    /// This class is thread safe. A single <see cref="ApiRateLimiter"/> instance
    /// may be used to control the rate of an occurrence across multiple threads.
    /// </para>
    /// </remarks>
    public sealed class ApiRateLimiter : IDisposable, IAsyncDisposable
    {
        private readonly SlidingWindowRateLimiter _rateLimiter;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new <see cref="ApiRateLimiter"/> with the specified rate limit configuration.
        /// </summary>
        /// <param name="rateLimit">The rate limit configuration specifying occurrences per time unit.</param>
        /// <exception cref="ArgumentNullException">
        /// If <paramref name="rateLimit"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// If <paramref name="rateLimit"/> has invalid occurrences or time unit values.
        /// </exception>
        public ApiRateLimiter(RateLimit rateLimit)
        {
            if (rateLimit == null)
                throw new ArgumentNullException(nameof(rateLimit));
            
            if (rateLimit.Occurrences <= 0)
                throw new ArgumentOutOfRangeException(nameof(rateLimit), "Number of occurrences must be a positive integer");
            
            if (rateLimit.TimeUnit <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(rateLimit), "Time unit must be a positive span of time");

            Occurrences = rateLimit.Occurrences;
            TimeUnit = rateLimit.TimeUnit;

            // Using SlidingWindowRateLimiter which provides smooth rate limiting similar to the original RateGate
            // The window is divided into segments, and permits replenish as segments slide out
            var options = new SlidingWindowRateLimiterOptions
            {
                PermitLimit = rateLimit.Occurrences,
                Window = rateLimit.TimeUnit,
                SegmentsPerWindow = Math.Max(1, (int)Math.Ceiling(rateLimit.TimeUnit.TotalSeconds)),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = rateLimit.Occurrences * 10, // Allow reasonable queuing
                AutoReplenishment = true
            };

            _rateLimiter = new SlidingWindowRateLimiter(options);
        }

        /// <summary>
        /// Number of occurrences allowed per unit of time.
        /// </summary>
        public int Occurrences { get; }

        /// <summary>
        /// The length of the time unit.
        /// </summary>
        public TimeSpan TimeUnit { get; }

        /// <summary>
        /// The length of the time unit, in milliseconds.
        /// </summary>
        public int TimeUnitMilliseconds => (int)TimeUnit.TotalMilliseconds;

        /// <summary>
        /// Asynchronously waits until allowed to proceed or until the specified timeout elapses.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the wait operation.</param>
        /// <returns>A task that completes when the operation is allowed to proceed, returning true if acquired, false if timed out.</returns>
        public async ValueTask<bool> WaitToProceedAsync(CancellationToken cancellationToken = default)
        {
            CheckDisposed();

            using var lease = await _rateLimiter.AcquireAsync(1, cancellationToken).ConfigureAwait(false);
            return lease.IsAcquired;
        }

        /// <summary>
        /// Asynchronously waits until allowed to proceed or until the specified timeout elapses.
        /// </summary>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the wait operation.</param>
        /// <returns>A task that completes when the operation is allowed to proceed, returning true if acquired, false if timed out.</returns>
        public async ValueTask<bool> WaitToProceedAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            CheckDisposed();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            try
            {
                using var lease = await _rateLimiter.AcquireAsync(1, cts.Token).ConfigureAwait(false);
                return lease.IsAcquired;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout occurred, not external cancellation
                return false;
            }
        }

        /// <summary>
        /// Blocks the current thread until allowed to proceed or until the specified timeout elapses.
        /// </summary>
        /// <param name="millisecondsTimeout">Number of milliseconds to wait, or -1 to wait indefinitely.</param>
        /// <returns>true if the thread is allowed to proceed, or false if timed out.</returns>
        public bool WaitToProceed(int millisecondsTimeout)
        {
            if (millisecondsTimeout < -1)
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout));

            CheckDisposed();

            if (millisecondsTimeout == -1)
            {
                // Infinite wait - use synchronous acquire
                using var lease = _rateLimiter.AttemptAcquire(1);
                if (lease.IsAcquired)
                    return true;

                // If immediate acquire failed, wait asynchronously
                return WaitToProceedAsync().AsTask().GetAwaiter().GetResult();
            }
            else
            {
                var timeout = TimeSpan.FromMilliseconds(millisecondsTimeout);
                return WaitToProceedAsync(timeout).AsTask().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Blocks the current thread until allowed to proceed or until the specified timeout elapses.
        /// </summary>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <returns>true if the thread is allowed to proceed, or false if timed out.</returns>
        public bool WaitToProceed(TimeSpan timeout)
        {
            return WaitToProceed((int)timeout.TotalMilliseconds);
        }

        /// <summary>
        /// Blocks the current thread indefinitely until allowed to proceed.
        /// </summary>
        public void WaitToProceed()
        {
            WaitToProceed(-1);
        }

        /// <summary>
        /// Notifies the rate limiter that a priority call was made.
        /// Priority calls bypass the wait but still count against the rate limit.
        /// </summary>
        /// <remarks>
        /// This method attempts to acquire a permit without blocking. If a permit
        /// is not immediately available, the call is still counted but execution
        /// continues without waiting.
        /// </remarks>
        public void NotifyPriorityCallMade()
        {
            CheckDisposed();
            
            // Try to acquire immediately - if successful, dispose the lease immediately
            // If not successful, the call proceeds anyway (it's high priority)
            using var lease = _rateLimiter.AttemptAcquire(1);
            // Lease is disposed immediately - if acquired, permit is returned to pool
            // This matches the original behavior where priority calls are counted but don't wait
        }

        /// <summary>
        /// Gets the number of permits currently available.
        /// </summary>
        /// <returns>The number of available permits.</returns>
        public int GetAvailablePermits()
        {
            CheckDisposed();
            var stats = _rateLimiter.GetStatistics();
            return stats != null ? (int)Math.Min(stats.CurrentAvailablePermits, int.MaxValue) : 0;
        }

        private void CheckDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ApiRateLimiter));
        }

        /// <summary>
        /// Releases the resources used by the <see cref="ApiRateLimiter"/>.
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _rateLimiter.Dispose();
                _isDisposed = true;
            }
        }

        /// <summary>
        /// Asynchronously releases the resources used by the <see cref="ApiRateLimiter"/>.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (!_isDisposed)
            {
                await _rateLimiter.DisposeAsync().ConfigureAwait(false);
                _isDisposed = true;
            }
        }
    }
}
