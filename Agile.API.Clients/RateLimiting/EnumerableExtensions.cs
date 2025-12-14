namespace Agile.API.Clients.RateLimiting
{
    /// <summary>
    /// Extension methods for rate limiting IEnumerable sequences.
    /// </summary>
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Limits the rate at which the sequence is enumerated.
        /// </summary>
        /// <typeparam name="T">The type of the elements of <paramref name="source" />.</typeparam>
        /// <param name="source">The <see cref="IEnumerable{T}" /> whose enumeration is to be rate limited.</param>
        /// <param name="rateLimit">The rate limit configuration.</param>
        /// <returns>An <see cref="IEnumerable{T}" /> containing the elements of the source sequence.</returns>
        public static IEnumerable<T> LimitRate<T>(this IEnumerable<T> source, RateLimit rateLimit)
        {
            using (var rateLimiter = new ApiRateLimiter(rateLimit))
            {
                foreach (var item in source)
                {
                    rateLimiter.WaitToProceed();
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Limits the rate at which the sequence is enumerated.
        /// </summary>
        /// <typeparam name="T">The type of the elements of <paramref name="source" />.</typeparam>
        /// <param name="source">The <see cref="IEnumerable{T}" /> whose enumeration is to be rate limited.</param>
        /// <param name="rateLimiter">The rate limiter instance to use.</param>
        /// <returns>An <see cref="IEnumerable{T}" /> containing the elements of the source sequence.</returns>
        /// <remarks>
        /// This overload does not dispose the rate limiter - the caller is responsible for disposing it.
        /// </remarks>
        public static IEnumerable<T> LimitRate<T>(this IEnumerable<T> source, ApiRateLimiter rateLimiter)
        {
            foreach (var item in source)
            {
                rateLimiter.WaitToProceed();
                yield return item;
            }
        }

#pragma warning disable CS0618 // Type or member is obsolete
        /// <summary>
        /// Limits the rate at which the sequence is enumerated using the legacy RateGate.
        /// </summary>
        /// <typeparam name="T">The type of the elements of <paramref name="source" />.</typeparam>
        /// <param name="source">The <see cref="IEnumerable{T}" /> whose enumeration is to be rate limited.</param>
        /// <param name="rateGate">The legacy rate gate instance to use.</param>
        /// <returns>An <see cref="IEnumerable{T}" /> containing the elements of the source sequence.</returns>
        /// <remarks>
        /// This overload is provided for backwards compatibility. Consider using 
        /// <see cref="LimitRate{T}(IEnumerable{T}, ApiRateLimiter)"/> instead.
        /// </remarks>
        [System.Obsolete("Use the overload with ApiRateLimiter instead. This method is provided for backwards compatibility.")]
        public static IEnumerable<T> LimitRate<T>(this IEnumerable<T> source, RateGate rateGate)
        {
            foreach (var item in source)
            {
                rateGate.WaitToProceed();
                yield return item;
            }
        }
#pragma warning restore CS0618 // Type or member is obsolete
    }
}