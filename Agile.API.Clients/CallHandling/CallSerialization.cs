using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;

namespace Agile.API.Clients.CallHandling
{
    public static class CallSerialization
    {
        private static JsonSerializer Serializer { get; } = new JsonSerializer();


        public static async Task<T> DeserializeJsonResponse<T>(HttpResponseMessage response)
        {
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            var jsonReader = new JsonTextReader(reader);
            return Serializer.Deserialize<T>(jsonReader);
        }


        public static async Task<string> ResponseAsString(HttpResponseMessage response)
        {
            return await ResponseAsString(response, NullLogger.Instance);
        }

        public static async Task<string> ResponseAsString(HttpResponseMessage response, ILogger logger)
        {
            try
            {
                using var stream = await response.Content.ReadAsStreamAsync();
                using var sr = new StreamReader(stream);

                return await sr.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deserializing HTTP response content");
                return "error deserializing";
            }
        }
    }
}