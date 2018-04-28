using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BOG.DropZone.Client.Model
{
    [JsonObject]
    public class Lockbox
    {
        [JsonProperty]
        public string ContentType { get; set; } = "text/plain";
        [JsonProperty]
        public string Content { get; set; } = string.Empty;
        [JsonProperty]
        public string MD5 { get; set; } = string.Empty;
    }
}
