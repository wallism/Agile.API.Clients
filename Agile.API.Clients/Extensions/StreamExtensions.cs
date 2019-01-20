using System;
using System.IO;
using System.Text;

namespace Agile.API.Client.Extensions
{
    public static class StreamExtensions
    {
        /// <summary>
        ///     Convert the stream to a string
        /// </summary>
        public static string StreamToString(this Stream stream)
        {
            try
            {
                if(stream.Position != 0)
                    stream.Position = 0;
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                return $"StreamToString FAILED. {ex.Message}";
            }
        }
    }
}