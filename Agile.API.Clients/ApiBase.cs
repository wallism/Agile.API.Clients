using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Agile.API.Clients.CallHandling;
using Agile.API.Clients.Helpers;
using PennedObjects.RateLimiting;

namespace Agile.API.Clients
{
    public abstract class ApiBase
    {
        private readonly HttpClient httpClient;
        private readonly RateGate rateGate;

        protected ApiBase(string apiKey, RateLimit rateLimit, string apiSecret = "")
        {
            ApiKey = apiKey;
            ApiSecret = apiSecret;

            HasRateLimit = rateLimit.HasLimit;
            rateGate = new RateGate(HasRateLimit
                ? rateLimit
                : RateLimit.Build(9999, TimeSpan.FromMilliseconds(1)));


            // one httpClient per api for now (may be better than one for all anyway)
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            httpClient = new HttpClient(handler);
        }

        protected string ApiKey { get; }

        protected string ApiSecret { get; }

        protected abstract string BaseUrl { get; }

        /// <summary>
        ///     Identifies which API it is (useful for logging)
        /// </summary>
        public abstract string ApiId { get; }


        public bool HasRateLimit { get; }


        public ApiMethod<T> PublicGet<T>(MethodPriority priority) where T : class
        {
            return PublicGet<T>(priority, MediaTypes.JSON);
        }

        public ApiMethod<T> PublicGet<T>(MethodPriority priority, MediaTypeHeaderValue contentType) where T : class
        {
            return new PublicMethod<T>(this, HttpMethod.Get, priority, contentType);
        }


        public ApiMethod<T> PrivateGet<T>(MethodPriority priority) where T : class
        {
            return PrivateGet<T>(priority, MediaTypes.JSON);
        }

        public ApiMethod<T> PrivateGet<T>(MethodPriority priority, MediaTypeHeaderValue contentType) where T : class
        {
            return new PrivateMethod<T>(this, HttpMethod.Get, priority, contentType);
        }


        public ApiMethod<T> PrivatePost<T>(MethodPriority priority) where T : class
        {
            return PrivatePost<T>(priority, MediaTypes.JSON);
        }

        public ApiMethod<T> PrivatePost<T>(MethodPriority priority, MediaTypeHeaderValue contentType) where T : class
        {
            return new PrivateMethod<T>(this, HttpMethod.Post, priority, contentType);
        }


        protected virtual long GetNonce()
        {
            return ServerTime.UnixTimeStampUtc();
        }

        protected virtual string GetPublicRequestUri(string path, string querystring = "")
        {
            // by default public is same as private, separate overload provided because for some API's they are indeed different.
            return GetPrivateRequestUri(path, querystring);
        }

        protected virtual string GetPrivateRequestUri(string path, string querystring = "")
        {
            var url = $"{BaseUrl}/{path}";
            return string.IsNullOrWhiteSpace(querystring)
                ? url
                : $"{url}?{querystring}";
        }


        protected virtual void SetPublicRequestProperties(HttpRequestMessage request, string method, object? rawPayload = null, string propsWithNonce = "")
        {
        }

        protected virtual void SetPrivateRequestProperties(HttpRequestMessage request, string method, object? rawPayload = null, string propsWithNonce = "")
        {
            throw new NotImplementedException("Required if calling private methods on the API");
        }


        private void PassThroughRateGate<T>(ApiMethod<T> method) where T : class
        {
            if (!HasRateLimit)
                return;

            if (method.IsHighPriority)
                rateGate.NotifyPriorityCallMade();
            else
                rateGate?.WaitToProceed();
        }

        /// <summary>
        ///     Implement a handler to do always do something (like log the error) when an error occurs.
        ///     Keep any action lightweight!
        /// </summary>
        /// <remarks>not actually logging here so this library does not require a ref to any logging libraries</remarks>
        protected virtual void NotifyError<T>(CallResult<T> result) where T : class
        {
            Debug.WriteLine($"{ApiId} {result.StatusCode}:{result.AbsoluteUri} {result.Exception?.Message ?? "no ex message"} | {result.RawText}");
        }


        public class PublicMethod<TResponse> : ApiMethod<TResponse> where TResponse : class
        {
            public PublicMethod(ApiBase api, HttpMethod httpMethod, MethodPriority priority)
                : this(api, httpMethod, priority, MediaTypes.JSON)
            {
            }

            public PublicMethod(ApiBase api, HttpMethod httpMethod, MethodPriority priority, MediaTypeHeaderValue contentType)
                : base(api, httpMethod, priority, contentType)
            {
            }


            //        public static ApiMethod<TResponse> Get(MethodPriority priority, string contentType = ContentTypes.JSON) => new PublicMethod<TResponse>(HttpMethod.Get, priority, contentType);
            //        public static ApiMethod<TResponse> Post(MethodPriority priority, string contentType = ContentTypes.JSON) => new PublicMethod<TResponse>(HttpMethod.Post, priority, contentType);

            protected override HttpRequestMessage CreateRequest(string path, string querystring, string payload)
            {
                var uri = Api.GetPublicRequestUri(path, querystring);

                var request = new HttpRequestMessage(HttpMethod, uri);
                Api.SetPublicRequestProperties(request, path, payload, querystring);

                // this adds a content body (POST only)
                AddPayloadToBody(request, payload);
                return request;
            }
        }

        public class PrivateMethod<TResponse> : ApiMethod<TResponse> where TResponse : class
        {
            public PrivateMethod(ApiBase api, HttpMethod httpMethod, MethodPriority priority, MediaTypeHeaderValue contentType)
                : base(api, httpMethod, priority, contentType)
            {
            }

            protected override HttpRequestMessage CreateRequest(string path, string querystring, string payload)
            {
                var uri = Api.GetPrivateRequestUri(path, querystring);

                var request = new HttpRequestMessage(HttpMethod, uri);
                Api.SetPrivateRequestProperties(request, path, payload, querystring);

                // this adds a content body (POST only)
                AddPayloadToBody(request, payload);
                return request;
            }
        }

        /// <summary>
        ///     Details about an API method. (only one per method should be instantiated)
        /// </summary>
        /// <remarks>
        ///     Single instance to be created for each method.
        ///     Also allows simplification in the ApiBase, main intent is to help improve readability
        /// </remarks>
        public abstract class ApiMethod<TResponse> where TResponse : class
        {
            public readonly HttpMethod HttpMethod;
            public readonly MediaTypeHeaderValue MethodContentType;


            protected ApiBase Api;

            /// <inheritdoc />
            protected ApiMethod(ApiBase api, HttpMethod httpMethod, MethodPriority priority, MediaTypeHeaderValue contentType)
            {
                Api = api;
                HttpMethod = httpMethod;
                MethodContentType = contentType;
                Priority = priority;
            }


            /// <summary>
            ///     High priority calls will not be stopped by the RateGate, they go straight through.
            ///     (note: The call still gets counted)
            /// </summary>
            public MethodPriority Priority { get; }


            public bool IsHighPriority => Priority == MethodPriority.High;


            public async Task<CallResult<TResponse>> Call(string path,
                string payload = "",
                string querystring = "",
                CancellationToken cancellationToken = default)
            {
                //            Console.WriteLine($"[Thread:{Thread.CurrentThread.ManagedThreadId}] {path}");
                var request = CreateRequest(path, payload, querystring);
                Api.PassThroughRateGate(this);


                HttpResponseMessage? response = null;
                var timer = Stopwatch.StartNew();
                // two separate (nested) try catch blocks because want to distinguish between an ex occuring making the call
                // and an ex occuring processing the response
                try
                {
                    response = await Api.httpClient.SendAsync(request, cancellationToken);
                    timer.Stop();

                    var result = await CallResult<TResponse>.Wrap(request, response, timer.ElapsedMilliseconds);

                    if (!result.WasSuccessful)
                        Api.NotifyError(result);
                    return result;
                }
                catch (Exception ex)
                {
                    timer.Stop();
                    var result = CallResult<TResponse>.BuildException(ex, request, timer.ElapsedMilliseconds);
                    Api.NotifyError(result);
                    return result;
                }
                finally
                {
                    response?.Dispose();
                }
            }


            protected abstract HttpRequestMessage CreateRequest(string path, string querystring, string payload);

            /// <summary>
            ///     Add the given payload to the body of the request
            /// </summary>
            protected void AddPayloadToBody(HttpRequestMessage request, string payload)
            {
                if (request.Method == HttpMethod.Get || string.IsNullOrWhiteSpace(payload))
                    return;

                request.Content = new StringContent(payload);
                request.Content.Headers.ContentType = MethodContentType;
            }
        }
    }
}