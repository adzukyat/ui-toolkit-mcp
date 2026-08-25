using Newtonsoft.Json;

namespace UIToolkitMcpPreviewServer.Protocol
{
    internal static class Json
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None
        };

        internal static string Serialize(object value)
        {
            return JsonConvert.SerializeObject(value, Settings);
        }

        internal static T Deserialize<T>(string value) where T : new()
        {
            if (string.IsNullOrEmpty(value) || value == "null")
                return new T();
            return JsonConvert.DeserializeObject<T>(value, Settings) ?? new T();
        }
    }
}
