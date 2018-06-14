using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace BOG.DropZone.Client.Entity
{
    [JsonObject]
    public class PayloadGram
    {
        [JsonProperty]
        public string Payload { get; set; } = string.Empty;
        [JsonProperty]
        public string Length { get; set; } = string.Empty;
        [JsonProperty]
        public string HashValidation { get; set; } = string.Empty;
        [JsonProperty]
        public bool IsEncrypted { get; set; } = false;
    }
}
