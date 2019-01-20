using System;
using System.Net;
using Agile.API.Client.Extensions;

namespace Agile.API.Client.CallHandling
{
    public class TextCallResult<T> : ServiceCallResult<T>
    {
        public TextCallResult(Uri requestUri, WebResponse response, long elapsedMilliseconds) 
            : base(requestUri, response, elapsedMilliseconds) {}

        public static ServiceCallResult<T> Build(Uri requestUri, WebResponse response, long elapsedMilliseconds)
        {
            try
            {
                var result = new TextCallResult<T>(requestUri, response, elapsedMilliseconds);
                result.DeserializeResponse(response);
                return result;
            }
            catch (Exception ex)
            {
                return CallErrorResult<T>.Build(ex, requestUri, response, elapsedMilliseconds);
            }
        }

        protected void DeserializeResponse(WebResponse response)
        {
            try
            {
                using (var responseStream = response.GetResponseStream())
                {
                    TextResponse = responseStream.StreamToString();
                }

                // log because a text response usually means something has gone wrong )
//                Log.Information("{TextResponse}", Value);
            }
            catch
            {
//                Log.Information("Failed to convert stream to string");
                // do nothing is fine, ex is already logged in the extension method
            }
        }
    }
}