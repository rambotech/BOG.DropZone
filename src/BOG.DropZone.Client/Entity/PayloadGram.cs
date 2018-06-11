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
        public int Length { get; set; } = 0;
        [JsonProperty]
        public string HashValidation { get; set; } = string.Empty;
        [JsonProperty]
        public bool IsEncrypted { get; set; } = false;
    }
}
