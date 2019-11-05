using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
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
            try
            {
                using var stream = await response.Content.ReadAsStreamAsync();
                using var sr = new StreamReader(stream);

                return await sr.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return "error deserializing";
            }

        }

    }
}