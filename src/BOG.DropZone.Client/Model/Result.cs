﻿using System;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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
            InvalidRequest = 1,

            /// <summary>
            /// The action succeeded, on one or all servers called.
            /// </summary>
            InvalidAuthentication = 2,

            /// <summary>
            /// No payload is available
            /// </summary>
            NoDataAvailable = 3,

            /// <summary>
            /// No space to store another reference or payload
            /// </summary>
            OverLimit = 4,

            /// <summary>
            /// Cryptography failure
            /// </summary>
            DataCompromised = 5,

            /// <summary>
            /// Error logged at server
            /// </summary>
            ServerError = 6,

            /// <summary>
            /// Failed to connect to the dropzone endpoint
            /// </summary>
            ConnectionFailed = 7,

            /// <summary>
            /// The response is unknown
            /// </summary>
            UnexpectedResponse = 8,

            /// <summary>
            /// Exception occurred in the client.
            /// </summary>
            Fatal = 9
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
        public string CastType { get; set; } = null;
    }
}
