using System.Collections.Generic;

namespace PennedObjects.RateLimiting
{
    public static class EnumerableExtensions
    {
        /// <summary>
        ///     Limits the rate at which the sequence is enumerated.
        /// </summary>
        /// <typeparam name="T">The type of the elements of <paramref name="source" />.</typeparam>
        /// <param name="source">The <see cref="IEnumerable{T}" /> whose enumeration is to be rate limited.</param>
        /// <param name="rateLimit"></param>
        /// <returns>An <see cref="IEnumerable{T}" /> containing the elements of the source sequence.</returns>
        public static IEnumerable<T> LimitRate<T>(this IEnumerable<T> source, RateLimit rateLimit)
        {
            using (var rateGate = new RateGate(rateLimit))
            {
                foreach (var item in source)
                {
                    rateGate.WaitToProceed();
                    yield return item;
                }
            }
        }
    }
}