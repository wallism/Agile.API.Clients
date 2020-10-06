using System;
using System.Threading.Tasks;
using Agile.API.Clients.CallHandling;
using System.Net.Http;
using Microsoft.Extensions.Configuration;

namespace Agile.API.Clients.Tests.Mocks
{
    public class WidgetApi : ApiBase
    {
        public WidgetApi(IConfiguration config) : base(config)
        {
            get = PublicGet<Widget>(MethodPriority.Normal);
            post = PrivatePost<Widget>(MethodPriority.Normal);

//            postWidget = RegisterMethod(ApiMethod.Post(MethodExposure.Public, MethodPriority.Normal));
        }


        protected override string BaseUrl => "http://mytestdomaindoesnotexist.com"; // don't use locahost
        public override string ApiId => "MOCK";


        private readonly ApiMethod<Widget> get;
        private readonly ApiMethod<Widget> post;


        public async Task<CallResult<Widget>> GetWidget(long widgetId)
        {
            return await get.Call($"widget/{widgetId}", string.Empty);
        }

        public async Task<CallResult<Widget>> GetWidget(Widget widget)
        {
            // TODO: serialize logic
            return await post.Call($"widget/{widget.Id}", "");
        }


        protected override Task SetPrivateRequestProperties(HttpRequestMessage request, string method, object rawPayload = null, string propsWithNonce = null)
        {
            throw new NotImplementedException("Required if calling private methods on the API");
        }


    }

    public class Widget
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}