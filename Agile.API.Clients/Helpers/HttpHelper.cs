using System.Net;

namespace Agile.API.Clients.Helpers
{
    /// <summary>
    /// http helper stuff
    /// </summary>
    public static class HttpHelper
    {

        public static bool IsSuccessStatusCode(this HttpStatusCode statusCode)
        {
            return (int) statusCode >= 200 && (int) statusCode <= 299;
        }

    }
}
