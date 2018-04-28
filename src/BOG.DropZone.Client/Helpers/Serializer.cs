using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace BOG.DropZone.Client.Helpers
{
    public static class Serializer<T> where T : class
    {
        public static T FromJson(string json) => JsonConvert.DeserializeObject<T>(json, Converter.Config);

        public static string ToJson(T obj) => JsonConvert.SerializeObject(obj, typeof(T), Converter.Config);
    }

    /// <summary>
    /// Standard serializtion settings for JSON.
    /// </summary>
    public static class Converter
    {
        public static readonly JsonSerializerSettings Config = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.DateTime,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };
    }
}