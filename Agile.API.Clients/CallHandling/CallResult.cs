using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Threading.Tasks;
using Agile.API.Clients.Helpers;
using Newtonsoft.Json;

namespace Agile.API.Clients.CallHandling
{
    /// <summary>
    ///     Wrapper so all calls to all APIs return the same type.
    ///     Also ensures important details/errors are not lost.
    ///     Possible outcomes of a call to an API:
    ///     Response received
    ///     - Success code
    ///     - return result
    ///     - exception creating result (e.g. issue with deserialization)
    ///     - Not Success Code
    ///     - return result with error details
    ///     - exception creating error result (issue with deserialization possibly)
    ///     Exception making call - no response received at all
    ///     - return result with error details
    /// </summary>
    public class CallResult<T> where T : class
    {
        protected CallResult(HttpResponseMessage? response, HttpRequestMessage request, long elapsedMilliseconds)
        {
            // response not stored on a property because it gets disposed.
            AbsoluteUri = request.RequestUri.AbsoluteUri;
            Elapsed = elapsedMilliseconds;

            if (response != null)
            {
                ContentType = response.Content.Headers.ContentType;
                StatusCode = response.StatusCode;
                IsSuccessStatusCode = response.IsSuccessStatusCode;
            }
        }


        private CallResult(T value, string responseString, HttpRequestMessage request, HttpResponseMessage response, long elapsedMilliseconds)
            : this(response, request, elapsedMilliseconds)
        {
            RawText = responseString;
            Value = value;
        }

        private CallResult(string value, HttpRequestMessage request, HttpResponseMessage response, long elapsedMilliseconds)
            : this(response, request, elapsedMilliseconds)
        {
            RawText = StringValue = value;
        }

        private CallResult(Exception ex, string raw, HttpRequestMessage request, HttpResponseMessage? response, long elapsedMilliseconds)
            : this(response, request, elapsedMilliseconds)
        {
            Exception = ex;
            RawText = raw;
        }
        private CallResult(Exception ex, string raw)
        {
            AbsoluteUri = "nocallmade";
            Exception = ex;
            RawText = raw;
        }

        public string AbsoluteUri { get; set; }

        /// <summary>
        ///     Gets the response ContentType
        /// </summary>
        public MediaTypeHeaderValue? ContentType { get; set; }

        /// <summary>
        ///     Gets the Response HttpStatusCode
        /// </summary>
        public HttpStatusCode? StatusCode { get; protected set; }


        /// <summary>
        ///     Returns true if the call was successful
        ///     and no exception occurred handling the result
        /// </summary>
        public bool WasSuccessful => Exception == null;

        /// <summary>
        ///     True if was true on the response
        /// </summary>
        /// <remarks>possible for this to be true and WasSuccessful false - means error handling result</remarks>
        public bool IsSuccessStatusCode { get; set; }


        /// <summary>
        ///     Gets the exception that occured when the call was made,
        ///     OR any error that occurs processing the result
        /// </summary>
        public Exception? Exception { get; }


        /// <summary>
        ///     Gets the deserialized value returned by the call.
        /// </summary>
        public T? Value { get; protected set; }

        /// <summary>
        ///     Gets the value returned by the call as a string - only for when ContentType is TEXT.
        /// </summary>
        public string? StringValue { get; protected set; }

        /// <summary>
        ///     Gets the text response that was returned instead of the expected Json.
        /// </summary>
        public string? RawText { get; set; }

        public long Elapsed { get; set; }


        public static CallResult<T> BuildException(Exception ex, HttpRequestMessage request, long elapsedMilliseconds)
        {
            return new CallResult<T>(ex, "no response", request, null, elapsedMilliseconds);
        }

        public static CallResult<T> BuildException(Exception ex, string raw)
        {
            return new CallResult<T>(ex, raw);
        }

        public static async Task<CallResult<T>> Wrap(HttpRequestMessage request, HttpResponseMessage response, long elapsedMilliseconds)
        {
            if (!response.IsSuccessStatusCode)
            {
                // response received but the call failed, expected response type unlikely to be in the response

                // todo: is there an error T defined? if yes, use that, otherwise just the string

                var raw = await CallSerialization.ResponseAsString(response) ?? "";
                return new CallResult<T>(new Exception($"StatusCode = {response.StatusCode} {raw}"), raw, request, response, elapsedMilliseconds);
            }

            var responseString = await CallSerialization.ResponseAsString(response);

            try
            {

                if (response.Content.Headers.ContentType == null)
                {
                    // some responses don't have a ContentType. The call was successful though
                    return new CallResult<T>((T)null, responseString, request, response, elapsedMilliseconds);
                }

                // Success status code
                if (response.Content.Headers.ContentType.MediaType.Equals(MediaTypes.JSON.MediaType))
                    try
                    {
                        T value = null;
                        // ContentType may be JSON but requested return type is string, therefore no deserialization required.
                        if (typeof(T) == typeof(string))
                        {
                            return new CallResult<T>(responseString, request, response, elapsedMilliseconds);
                        }

                        value = JsonConvert.DeserializeObject<T>(responseString) ?? throw new InvalidOperationException("deserializingT");
                        return new CallResult<T>(value, responseString, request, response, elapsedMilliseconds);
                    }
                    catch (Exception exception)
                    {
                        // try get the raw text
                        return new CallResult<T>(exception, responseString, request, response, elapsedMilliseconds);
                    }


                if (response.Content.Headers.ContentType.MediaType.Equals(MediaTypes.TEXT.MediaType))
                {
                    return new CallResult<T>(responseString, request, response, elapsedMilliseconds);
                }

                return new CallResult<T>(new Exception($"Response from {request.RequestUri.AbsoluteUri} returned an unsupported ContentType {response.Content.Headers.ContentType.MediaType}"),
                    responseString, request, response, elapsedMilliseconds);
            }
            catch (Exception ex)
            {
                // ex probably occurred deserializing from json.
                return new CallResult<T>(ex, responseString, request, response, elapsedMilliseconds);
            }
        }
    }
}