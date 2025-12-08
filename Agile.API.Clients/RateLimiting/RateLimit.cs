using System;

namespace PennedObjects.RateLimiting
{
    /// <summary>
    /// Represents a rate limit configuration specifying the number of occurrences 
    /// allowed per time unit.
    /// </summary>
    public class RateLimit
    {
        /// <summary>
        /// Creates a new rate limit configuration.
        /// </summary>
        /// <param name="occurrences">
        ///     The number of items in the sequence that are allowed to be processed per time unit.
        ///     Set to 0 for no limit.
        /// </param>
        /// <param name="timeUnit">Length of the time unit.</param>
        private RateLimit(int occurrences, TimeSpan timeUnit)
        {
            Occurrences = occurrences;
            TimeUnit = timeUnit;
        }

        /// <summary>
        /// Gets the number of occurrences allowed per time unit.
        /// </summary>
        public int Occurrences { get; }

        /// <summary>
        /// Gets the time unit duration.
        /// </summary>
        public TimeSpan TimeUnit { get; }

        /// <summary>
        /// Gets whether this rate limit has an actual limit (occurrences > 0).
        /// </summary>
        public bool HasLimit => Occurrences != 0;

        /// <summary>
        /// Creates a new rate limit configuration.
        /// </summary>
        /// <param name="occurrences">The number of occurrences allowed per time unit.</param>
        /// <param name="timeUnit">The time unit duration.</param>
        /// <returns>A new <see cref="RateLimit"/> instance.</returns>
        public static RateLimit Build(int occurrences, TimeSpan timeUnit)
        {
            return new RateLimit(occurrences, timeUnit);
        }

        /// <summary>
        /// Creates a new <see cref="ApiRateLimiter"/> from this rate limit configuration.
        /// </summary>
        /// <returns>A new <see cref="ApiRateLimiter"/> instance configured with this rate limit.</returns>
        public ApiRateLimiter CreateRateLimiter()
        {
            return new ApiRateLimiter(this);
        }
    }
}