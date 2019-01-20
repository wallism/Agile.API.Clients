using System;
using System.Diagnostics;
using System.Net;

namespace Agile.API.Client.CallHandling
{

    /// <summary>
    ///     Wrapper for all service call results.
    /// </summary>
    public abstract class ServiceCallResult<T>
    {
        protected ServiceCallResult(Uri requestUri, WebResponse response, long elapsedMilliseconds)
        {
            ContentType = "notSet";
            RequestUri = requestUri;
            Elapsed = elapsedMilliseconds;

            ContentType = response?.ContentType;
            if (response is HttpWebResponse httpResponse)
            {
                StatusCode = httpResponse.StatusCode;
            }
        }

        /// <summary>
        ///     Construct an 'ERROR' response
        /// </summary>
        protected ServiceCallResult(Exception ex, Uri requestUri, WebResponse response, long elapsedMilliseconds)
            : this(requestUri, response, elapsedMilliseconds)
        {
            Exception = ex;
        }

        
        public Uri RequestUri { get; }

        /// <summary>
        ///     Gets the response ContentType
        /// </summary>
        public string ContentType { get; }

        /// <summary>
        ///     Gets the Response HttpStatusCode
        /// </summary>
        public HttpStatusCode? StatusCode { get; protected set; }

        /// <summary>
        /// Returns true if the call was successful
        /// and no exception occurred handling the result
        /// </summary>
        public bool WasSuccessful => Exception == null;

        /// <summary>
        /// Gets the exception that occured when the call was made,
        /// OR any error that occurs processing the result
        /// </summary>
        public Exception Exception { get; protected set; }
        

        /// <summary>
        ///     Gets the deserialized value returned by the call.
        /// </summary>
        public T Value { get; protected set; }

        /// <summary>
        /// Gets the text response that was returned instead of the expected Json.
        /// If T is string then this gets populated instead of Value
        /// </summary>
        public string TextResponse { get; protected set; }

        public string RawText { get; set; }

        public long Elapsed { get; set; }

    }
}