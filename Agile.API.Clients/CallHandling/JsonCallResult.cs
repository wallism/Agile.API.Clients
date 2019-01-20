using System;
using System.IO;
using System.Net;
using Agile.API.Client.Extensions;
using Newtonsoft.Json;

namespace Agile.API.Client.CallHandling
{
    public class JsonCallResult<T> : ServiceCallResult<T>
    {
        private JsonCallResult(Uri requestUri, WebResponse response, long elapsedMilliseconds) 
            : base(requestUri, response, elapsedMilliseconds){}

        public static ServiceCallResult<T> Build(Uri requestUri, WebResponse response, long elapsedMilliseconds)
        {
            try
            {
                var result = new JsonCallResult<T>(requestUri, response, elapsedMilliseconds);
                result.DeserializeResponse(response);
                return result;
            }
            catch (Exception ex)
            {
                return CallErrorResult<T>.Build(ex, requestUri, response, elapsedMilliseconds);
            }
        }

        public static JsonSerializer Serializer { get; } = new JsonSerializer();


        protected void DeserializeResponse(WebResponse response)
        {

            using (var responseStream = response.GetResponseStream())
            {
                try
                {
                    // ReSharper disable once AssignNullToNotNullAttribute
                    using (var reader = new StreamReader(responseStream))
                    {
                        var jsonReader = new JsonTextReader(reader);
                        Value = Serializer.Deserialize<T>(jsonReader);
                    }
                }
                catch (Exception ex)
                {
                    Exception = ex;
                    if (responseStream != null)
                    {
                        responseStream.Position = 0;
                        RawText = responseStream.StreamToString();
                    }
                    
                }
            }
        }
    }
}