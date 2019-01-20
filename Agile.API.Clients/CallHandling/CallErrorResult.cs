using System;
using System.Net;

namespace Agile.API.Client.CallHandling
{
    /// <summary>
    /// There was an error calling the API
    /// </summary>
    public class CallErrorResult<T> : ServiceCallResult<T>
    {
        private CallErrorResult(Exception ex, Uri requestUri, WebResponse response, long elapsedMilliseconds)
            : base(ex, requestUri, response, elapsedMilliseconds)
        {
            WebException = Exception as WebException;
            if (WebException != null)
            {
                StatusCode = (WebException.Response as HttpWebResponse)?.StatusCode;
                WebExceptionStatus = WebException.Status;

            }
        }

        public static ServiceCallResult<T> Build(Exception ex, Uri requestUri, WebResponse response, long elapsedMilliseconds)
        {
            return new CallErrorResult<T>(ex, requestUri, response, elapsedMilliseconds);
        }

        /// <summary>
        ///  Gets the Exception cast to a WebEx (most of the time will be a WebEx)
        /// </summary>
        public WebException WebException { get; }

        public WebExceptionStatus WebExceptionStatus { get; }

    }
}