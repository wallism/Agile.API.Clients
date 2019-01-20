namespace Agile.API.Client
{
    /// <summary>
    /// Details about an API method. (only one per method should be instantiated)
    /// </summary>
    /// <remarks>Single instance to be created for each method.
    /// Also allows simplification in the ApiBase, main intent is to help improve readability</remarks>
    public class ApiMethod
    {
        private ApiMethod(bool isPublic, int timeout, bool prioritized)
        {
            IsPublic = isPublic;
            Prioritized = prioritized;
            TimeoutMS = timeout;
        }
        /// <summary>
        /// Secured, requires authentication logic
        /// </summary>
        public static ApiMethod Private(bool prioritized, int timeout = 59000)
        => new ApiMethod(false, timeout, prioritized);

        public static ApiMethod Public(bool prioritized, int timeout = 59000)
            => new ApiMethod(true, timeout, prioritized);

        /// <summary>
        /// If true, the endpoint does not require any authentication
        /// </summary>
        public bool IsPublic { get; }

        /// <summary>
        /// Prioritized calls will not be stopped by the RateGate, they go straight through.
        /// (note: The call still gets counted)
        /// </summary>
        public bool Prioritized { get; }

        /// <summary>
        /// Timeout to use, n milliseconds
        /// </summary>
        public int TimeoutMS { get; set; }
    }
    
}