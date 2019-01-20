using System;

namespace PennedObjects.RateLimiting
{
    public class RateLimit
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="occurrences">The number of items in the sequence that are allowed to be processed per time unit.</param>
        /// <param name="timeUnit">Length of the time unit.</param>
        private RateLimit(int occurrences, TimeSpan timeUnit)
        {
            Occurrences = occurrences;
            TimeUnit = timeUnit;
        }

        public static RateLimit Build(int occurrences, TimeSpan timeUnit)
            => new RateLimit(occurrences, timeUnit);

        public int Occurrences { get;  }

        public TimeSpan TimeUnit { get; }
    }
}