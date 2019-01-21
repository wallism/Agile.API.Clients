using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Agile.API.Client.CallHandling;
using Agile.API.Client.Helpers;
using PennedObjects.RateLimiting;

namespace Agile.API.Client
{
    public abstract class ApiBase
    {
        protected ApiBase(string apiKey, string apiSecret = null, RateLimit rateLimit = null)
        {
            ApiKey = apiKey;
            ApiSecret = apiSecret;
            if (rateLimit != null)
                rateGate = new RateGate(rateLimit);
        }

        protected string ApiKey { get; }

        protected string ApiSecret { get; }

        protected abstract string BaseUrl { get; }
        /// <summary>
        /// Identifies which API it is (useful for logging)
        /// </summary>
        public abstract string Code { get; }
        
        private RateGate rateGate;
        public bool HasRateLimit => rateGate != null;

        protected virtual string GetPublicRequestAddress(string path, string querystring = null)
        {
            // by default public is same as private, separate overload provided because for some API's they are indeed different.
            return GetPrivateRequestAddress(path, querystring);
        }

        protected virtual string GetPrivateRequestAddress(string path, string querystring = null)
        {
            var url = $"{BaseUrl}/{path}";
            return string.IsNullOrWhiteSpace(querystring)
                ? url
                : $"{url}?{querystring}";
        }

        protected virtual long GetNonce()
        {
            return ServerTime.UnixTimeStampUtc();
        }

        protected virtual void SetPublicRequestProperties(HttpWebRequest request)
        {
            AddDefaultHeaders(request);
        }

        protected virtual void SetPrivateRequestProperties(HttpWebRequest request, string method, object rawPayload = null, string propsWithNonce = null)
        {
            AddDefaultHeaders(request);
        }

        private static void AddDefaultHeaders(HttpWebRequest request)
        {
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        }

        
        protected async Task<ServiceCallResult<T>> Get<T>(ApiMethod apiMethod,
            string path,
            string querystring = null)
        {
            return await Call<T>("GET", apiMethod, path, null, querystring);
        }

        protected async Task<ServiceCallResult<T>> Post<T>(ApiMethod apiMethod,
            string path,
            string payload = null,
            string querystring = null)
        {
            return await Call<T>("POST", apiMethod, path, payload, querystring);
        }

        protected async Task<ServiceCallResult<T>> Put<T>(ApiMethod apiMethod,
            string path,
            string payload = null,
            string querystring = null)
        {
            return await Call<T>("PUT", apiMethod, path, payload, querystring);
        }

        private async Task<ServiceCallResult<T>> Call<T>(string verb,
            ApiMethod method,
            string path,
            string payload = null,
            string querystring = null)
        {
//            Console.WriteLine($"[Thread:{Thread.CurrentThread.ManagedThreadId}] {path}");
            var request = CreateRequest(verb, method, path, payload, querystring);

            if (HasRateLimit)
            {
                if (method.Prioritized)
                    rateGate.NotifyPriorityCallMade();
                else
                    rateGate?.WaitToProceed();
            }
            
            WebResponse response = null;
            var timer = Stopwatch.StartNew();
            try
            {
                // response disposed in finally
                response = await request.GetResponseAsync();
                timer.Stop();
                var result = Handle<T>(request.RequestUri, response, timer.ElapsedMilliseconds);
                NotifyIfError(result);
                return result;
            }
            catch (Exception ex)
            {
                timer.Stop();
                // error logging happens in side the ServiceCallResult
                var result = Handle<T>(ex, request.RequestUri, response, timer.ElapsedMilliseconds);
                NotifyIfError(result);
                return result;
            }
            finally
            {
                response?.Dispose();
            }
        }

        protected virtual void NotifyErrorHandler<T>(ServiceCallResult<T> errorResult)
        {
            Debug.WriteLine(errorResult.Exception);
        }

        private void NotifyIfError<T>(ServiceCallResult<T> result)
        {
            try
            {
                if(result.WasSuccessful)
                    return;
                NotifyErrorHandler(result);
            }
            catch (Exception)
            {
                // ignored - never want NotifyError to throw its own error
            }
        }


        private static ServiceCallResult<T> Handle<T>(Exception ex, Uri requestUri, WebResponse response, long elapsedMilliseconds)
        {
            return CallErrorResult<T>.Build(ex, requestUri, response, elapsedMilliseconds);
        }

        private static ServiceCallResult<T> Handle<T>(Uri requestUri, WebResponse response, long elapsedMilliseconds)
        {
            ServiceCallResult<T> result = null;
            if (response.ContentType.StartsWith(ContentTypes.JSON, StringComparison.CurrentCultureIgnoreCase))
            {
                result = JsonCallResult<T>.Build(requestUri, response, elapsedMilliseconds);
            }
            if (response.ContentType.StartsWith(ContentTypes.TEXT, StringComparison.CurrentCultureIgnoreCase))
            {
                result = TextCallResult<T>.Build(requestUri, response, elapsedMilliseconds);
            }

            return result;
        }

        private HttpWebRequest CreateRequest(string verb, ApiMethod method, string path, string payload, string querystring)
        {
            var address = (method.IsPublic)
                ? GetPublicRequestAddress(path, querystring)
                : GetPrivateRequestAddress(path, querystring);

            var request = (HttpWebRequest) WebRequest.Create(address);
            request.Method = verb;
            request.Timeout = method.TimeoutMS;

            if (method.IsPublic)
                SetPublicRequestProperties(request);
            else
                SetPrivateRequestProperties(request, path, payload, querystring);

            // this adds a content body (POST only)
            AddPayloadToBody(request, payload);
            return request;
        }
        


        /// <summary>
        ///  Add the given payload to the body of the request
        /// </summary>
        protected void AddPayloadToBody(HttpWebRequest webRequest, string payload)
        {
            if (webRequest.Method == "GET" || payload == null)
                return;

            using (var writer = new StreamWriter(webRequest.GetRequestStream()))
            {
                writer.Write(payload);
            }

        }
    }
}