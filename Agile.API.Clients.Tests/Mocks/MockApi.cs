using System.Threading.Tasks;
using Agile.API.Client.CallHandling;

namespace Agile.API.Client.Tests.Mocks
{
    public class MockApi : ApiBase
    {
        public MockApi() : base()
        {
        }


        protected override string BaseUrl => "http://localhost";
        public override string Code => "MOCK";

        private readonly ApiMethod getWidget = ApiMethod.Public(false);


        public async Task<ServiceCallResult<Widget>> GetWidget(long widgetId)
        {
            return await Get<Widget>(getWidget, $"widget/{widgetId}");
        }

    }

    public class Widget
    {
        public string Name { get; set; }
    }
}