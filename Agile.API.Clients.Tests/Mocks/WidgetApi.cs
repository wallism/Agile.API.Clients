using System.Threading.Tasks;
using Agile.API.Clients.CallHandling;
using PennedObjects.RateLimiting;
using System.Net.Http;

namespace Agile.API.Clients.Tests.Mocks
{
    public class WidgetApi : ApiBase
    {
        public WidgetApi(AuthOptions auth, RateLimit rateLimit = null) 
            : base(auth, rateLimit)
        {
            get = PublicGet<Widget>(MethodPriority.Normal);
            post = PrivatePost<Widget>(MethodPriority.Normal);

//            postWidget = RegisterMethod(ApiMethod.Post(MethodExposure.Public, MethodPriority.Normal));
        }


        protected override string BaseUrl => "http://localhost";
        public override string ApiId => "MOCK";


        private readonly ApiMethod<Widget> get;
        private readonly ApiMethod<Widget> post;


        public async Task<CallResult<Widget>> GetWidget(long widgetId)
        {
            return await get.Call($"widget/{widgetId}");
        }

        public async Task<CallResult<Widget>> GetWidget(Widget widget)
        {
            // TODO: serialize logic
            return await post.Call($"widget/{widget.Id}", "");
        }


        protected override void SetPrivateRequestProperties(HttpRequestMessage request, string method, object rawPayload = null, string propsWithNonce = null)
        {
            // add authentication headers etc
        }


    }

    public class Widget
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}