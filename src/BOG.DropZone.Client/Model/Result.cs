using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace BOG.DropZone.Client.Model
{
    [JsonObject]
    public class Result
    {
        public enum State : int
        {
            OK = 0,
            NoDataAvailable = 1,
            OverLimit = 2,
            DataCompromised = 3,
            ServerError = 4,
            ConnectionFailed = 5,
            UnexpectedResponse = 6,
            Fatal = 7
        };

        [JsonProperty]
        [JsonConverter(typeof(StringEnumConverter))]
        public State HandleAs { get; set; } = State.OK;

        [JsonProperty]
        [JsonConverter(typeof(StringEnumConverter))]
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.Unused;

        [JsonProperty]
        public string Message { get; set; } = null;

        [JsonProperty]
        public Exception Exception  { get; set; } = null;

        [JsonProperty]
        public string Content { get; set; } = null;
    }
}
