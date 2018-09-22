using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace BOG.DropZone.Client.Model
{
    /// <summary>
    /// The result object from a RestAPI call to a DropZone method.
    /// </summary>
    [JsonObject]
    public class Result
    {
        /// <summary>
        /// The descriptor of success/fail for a call.
        /// </summary>
        public enum State : int
        {
            /// <summary>
            /// The action succeeded, on one or all servers called.
            /// </summary>
            OK = 0,

            /// <summary>
            /// The action succeeded, on one or all servers called.
            /// </summary>
            InvalidAuthentication = 1,

            /// <summary>
            /// No payload is available
            /// </summary>
            NoDataAvailable = 2,

            /// <summary>
            /// No space to store another reference or payload
            /// </summary>
            OverLimit = 3,

            /// <summary>
            /// Cryptography failure
            /// </summary>
            DataCompromised = 4,

            /// <summary>
            /// Error logged at server
            /// </summary>
            ServerError = 5,

            /// <summary>
            /// Failed to connect to the dropzone endpoint
            /// </summary>
            ConnectionFailed = 6,

            /// <summary>
            /// The response is unknown
            /// </summary>
            UnexpectedResponse = 7,

            /// <summary>
            /// Exception occurred in the client.
            /// </summary>
            Fatal = 8
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

        [JsonProperty]
        public string ContentType { get; set; } = null;
    }
}
