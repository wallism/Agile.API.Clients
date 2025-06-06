using System.Diagnostics;
using System.Net.Http.Headers;
using Agile.API.Clients.CallHandling;
using Agile.API.Clients.Helpers;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using PennedObjects.RateLimiting;

namespace Agile.API.Clients
{
    public abstract class ApiBase
    {
        protected IConfiguration Configuration { get; }
        private readonly IHttpClientFactory _httpClientFactory;
        private HttpClient? httpClient;

        protected ApiBase(IConfiguration configuration, 
            IHttpClientFactory httpClientFactory)
        {
            Configuration = configuration;
            _httpClientFactory = httpClientFactory;

            var occurrences = configuration[$"APIS:{ApiId}:RateLimit:Occurrences"];
            RateGateOccurrences = string.IsNullOrEmpty(occurrences) ? "10" : occurrences;
            var seconds = configuration[$"APIS:{ApiId}:RateLimit:Seconds"];
            RateGateSeconds = string.IsNullOrEmpty(seconds) ? "1" : seconds;

            RateGate = new RateGate(RateLimit.Build(int.Parse(RateGateOccurrences),
                TimeSpan.FromSeconds(int.Parse(RateGateSeconds))));

            HasRateLimit = true; // force on by default, may be overwritten in inheritors
        }


        protected void SetAuthorizationHeader(AuthenticationHeaderValue header)
        {
            HttpClient.DefaultRequestHeaders.Authorization = header;
        }

        public bool HasRateLimit { get; protected set; }
        protected string RateGateOccurrences { get; set; }
        protected string RateGateSeconds { get; set; }

        private HttpClient HttpClient => httpClient ??= _httpClientFactory.CreateClient(HttpClientName);


        private RateGate RateGate { get; set; }

        protected string ApiKey { get; private set; }

        protected string ApiSecret { get; private set; }

        protected abstract string BaseUrl { get; }

        /// <summary>
        ///     Identifies which API it is (useful for logging)
        /// </summary>
        public abstract string ApiId { get; }

        protected virtual string HttpClientName { get; set; }  = DefaultHttpClientName;
        public const string DefaultHttpClientName = "DefaultHttpClient";

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

        public ApiMethod<T> PrivateDelete<T>(MethodPriority priority) where T : class
        {
            return new PrivateMethod<T>(this, HttpMethod.Delete, priority, MediaTypes.JSON);
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
            var url = path.StartsWith(BaseUrl) // allow full url to be passed (useful for pagination)
                ? path
                : $"{BaseUrl}/{path}";
            return string.IsNullOrWhiteSpace(querystring)
                ? url
                : $"{url}?{querystring}";
        }


        protected virtual async Task SetPublicRequestProperties(HttpRequestMessage request, string method, object? rawPayload = null, string propsWithNonce = "")
        {
            try
            {
                request.Headers.Host = request.RequestUri.Host;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        protected virtual async Task SetPrivateRequestProperties(HttpRequestMessage request, string method, object? rawPayload = null, string propsWithNonce = "")
        {
            throw new NotImplementedException("Required if calling private methods on the API");
        }


        private void PassThroughRateGate<T>(ApiMethod<T> method) where T : class
        {
            if (method.IsHighPriority)
                RateGate.NotifyPriorityCallMade();
            else
                RateGate?.WaitToProceed();
        }

        /// <summary>
        ///     Implement a handler to do always do something (like log the error) when an error occurs.
        ///     Keep any action lightweight!
        /// </summary>
        /// <remarks>not actually logging here so this library does not require a ref to any logging libraries</remarks>
        protected virtual void NotifyError<T>(CallResult<T> result) where T : class
        {
            var message = $"{ApiId} {result.StatusCode}:{result.AbsoluteUri} {result.Exception?.Message ?? "no ex message"} | {result.RawText}";
            Debug.WriteLine(message);
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

            protected override async Task<HttpRequestMessage> CreateRequest<T>(string path, string querystring, T payload)
            {
                var uri = Api.GetPublicRequestUri(path, querystring);

                var request = new HttpRequestMessage(HttpMethod, uri);
                await Api.SetPublicRequestProperties(request, path, payload, querystring);

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

            protected override async Task<HttpRequestMessage> CreateRequest<T>(string path, string querystring, T payload)
            {
                var uri = Api.GetPrivateRequestUri(path, querystring);

                var request = new HttpRequestMessage(HttpMethod, uri);
                await Api.SetPrivateRequestProperties(request, path, payload, querystring);

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


            public async Task<CallResult<TResponse>> Call<T>(string path,
                T payload,
                string querystring = "",
                CancellationToken cancellationToken = default)
            {
                //            Console.WriteLine($"[Thread:{Thread.CurrentThread.ManagedThreadId}] {path}");
                var request = await CreateRequest(path, querystring, payload);
                Api.PassThroughRateGate(this);

                HttpResponseMessage? response = null;
                var timer = Stopwatch.StartNew();
                // two separate (nested) try catch blocks because want to distinguish between an ex occuring making the call
                // and an ex occuring processing the response
                try
                {
                    response = await Api.HttpClient.SendAsync(request, cancellationToken);
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


            protected abstract Task<HttpRequestMessage> CreateRequest<T>(string path, string querystring, T payload);

            /// <summary>
            ///     Add the given payload to the body of the request
            /// </summary>
            protected void AddPayloadToBody<T>(HttpRequestMessage request, T payload)
            {
                if (request.Method == HttpMethod.Get || payload == null)
                    return;

                if (Equals(MethodContentType, MediaTypes.FormUrlEncoded))
                {
                    var formData = (IEnumerable<KeyValuePair<string, string>>)payload;
                    request.Content = new FormUrlEncodedContent(formData);
                }
                else if(payload is string stringPayload)
                {
                    request.Content = new StringContent(stringPayload);
                }
                else
                {
                    var serializedPayload = JsonConvert.SerializeObject(payload);
                    request.Content = new StringContent(serializedPayload);
                }

                request.Content.Headers.ContentType = MethodContentType;
            }
        }
    }
}