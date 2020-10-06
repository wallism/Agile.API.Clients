using System.Net.Http.Headers;

namespace Agile.API.Clients.Helpers
{
    /// <summary>
    /// Content Type
    /// </summary>
    public static class MediaTypes
    {
        public static MediaTypeHeaderValue JSON = new MediaTypeHeaderValue("application/json");
        public static MediaTypeHeaderValue TEXT = new MediaTypeHeaderValue("text/plain");
        public static MediaTypeHeaderValue XML = new MediaTypeHeaderValue("application/xml");
        public static MediaTypeHeaderValue HTML = new MediaTypeHeaderValue("text/html");
        public static MediaTypeHeaderValue JPEG = new MediaTypeHeaderValue("image/jpeg");
        public static MediaTypeHeaderValue FormUrlEncoded = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
    }
}