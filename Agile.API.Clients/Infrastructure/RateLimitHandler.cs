using Microsoft.Extensions.Configuration;
using PennedObjects.RateLimiting;

namespace Agile.API.Clients.Infrastructure
{
    /// <summary>
    /// Default implementation of <see cref="IRateLimitHandler"/> using <see cref="ApiRateLimiter"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This handler wraps the ApiRateLimiter to provide an async-first interface
    /// for rate limiting, avoiding thread pool starvation in async contexts.
    /// </para>
    /// <para>
    /// Rate limit configuration is read from IConfiguration using the pattern:
    /// APIS:{apiId}:RateLimit:Occurrences and APIS:{apiId}:RateLimit:Seconds
    /// </para>
    /// </remarks>
    public sealed class RateLimitHandler : IRateLimitHandler
    {
        private readonly ApiRateLimiter? _rateLimiter;
        private bool _isDisposed;

        /// <summary>
        /// Default number of occurrences allowed per time unit when not configured.
        /// </summary>
        public const int DefaultOccurrences = 10;

        /// <summary>
        /// Default time unit in seconds when not configured.
        /// </summary>
        public const int DefaultSeconds = 1;

        /// <summary>
        /// Initializes a new instance with rate limiting enabled using the specified configuration.
        /// </summary>
        /// <param name="configuration">The configuration source for rate limit settings.</param>
        /// <param name="apiId">The API identifier used to lookup configuration.</param>
        /// <exception cref="ArgumentNullException">Thrown when configuration or apiId is null.</exception>
        public RateLimitHandler(IConfiguration configuration, string apiId)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentException.ThrowIfNullOrWhiteSpace(apiId);

            var (occurrences, seconds) = ParseConfiguration(configuration, apiId);
            
            _rateLimiter = new ApiRateLimiter(RateLimit.Build(occurrences, TimeSpan.FromSeconds(seconds)));
            IsEnabled = true;
            Occurrences = occurrences;
            TimeUnitSeconds = seconds;
        }

        /// <summary>
        /// Initializes a new instance with explicit rate limit values.
        /// </summary>
        /// <param name="occurrences">Number of occurrences allowed per time unit.</param>
        /// <param name="timeUnitSeconds">The time unit in seconds.</param>
        public RateLimitHandler(int occurrences, int timeUnitSeconds)
        {
            if (occurrences <= 0)
                throw new ArgumentOutOfRangeException(nameof(occurrences), "Occurrences must be positive.");
            if (timeUnitSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeUnitSeconds), "Time unit must be positive.");

            _rateLimiter = new ApiRateLimiter(RateLimit.Build(occurrences, TimeSpan.FromSeconds(timeUnitSeconds)));
            IsEnabled = true;
            Occurrences = occurrences;
            TimeUnitSeconds = timeUnitSeconds;
        }

        /// <summary>
        /// Initializes a disabled rate limit handler (no rate limiting applied).
        /// </summary>
        private RateLimitHandler()
        {
            _rateLimiter = null;
            IsEnabled = false;
            Occurrences = 0;
            TimeUnitSeconds = 0;
        }

        /// <summary>
        /// Creates a disabled rate limit handler that performs no rate limiting.
        /// </summary>
        /// <returns>A disabled <see cref="RateLimitHandler"/>.</returns>
        public static RateLimitHandler CreateDisabled() => new();

        /// <inheritdoc />
        public bool IsEnabled { get; }

        /// <summary>
        /// Gets the configured number of occurrences per time unit.
        /// </summary>
        public int Occurrences { get; }

        /// <summary>
        /// Gets the configured time unit in seconds.
        /// </summary>
        public int TimeUnitSeconds { get; }

        /// <inheritdoc />
        public async ValueTask WaitToProceedAsync(CancellationToken cancellationToken = default)
        {
            if (!IsEnabled || _rateLimiter is null)
                return;

            ObjectDisposedException.ThrowIf(_isDisposed, this);

            await _rateLimiter.WaitToProceedAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void NotifyPriorityCall()
        {
            if (!IsEnabled || _rateLimiter is null)
                return;

            ObjectDisposedException.ThrowIf(_isDisposed, this);

            _rateLimiter.NotifyPriorityCallMade();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _rateLimiter?.Dispose();
            _isDisposed = true;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;

            if (_rateLimiter is not null)
            {
                await _rateLimiter.DisposeAsync().ConfigureAwait(false);
            }
            
            _isDisposed = true;
        }

        private static (int occurrences, int seconds) ParseConfiguration(IConfiguration configuration, string apiId)
        {
            var occurrencesStr = configuration[$"APIS:{apiId}:RateLimit:Occurrences"];
            var secondsStr = configuration[$"APIS:{apiId}:RateLimit:Seconds"];

            var occurrences = TryParsePositiveInt(occurrencesStr, DefaultOccurrences);
            var seconds = TryParsePositiveInt(secondsStr, DefaultSeconds);

            return (occurrences, seconds);
        }

        private static int TryParsePositiveInt(string? value, int defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            return int.TryParse(value, out var parsed) && parsed > 0 
                ? parsed 
                : defaultValue;
        }
    }
}
