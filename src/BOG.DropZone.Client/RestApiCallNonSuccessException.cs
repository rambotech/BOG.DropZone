using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace BOG.DropZone.Client
{
    public class RestApiCallNonSuccessException : Exception
    {
        public HttpStatusCode StatusCode { get; set; }
        public string ReasonPhrase { get; set; }

        public RestApiCallNonSuccessException(HttpStatusCode statusCode, string reasonPhrase)
        {
            StatusCode = statusCode;
            ReasonPhrase = reasonPhrase;
        }

        public RestApiCallNonSuccessException(HttpStatusCode statusCode, string reasonPhrase, string message)
            : base(message)
        {
            StatusCode = statusCode;
            ReasonPhrase = reasonPhrase;
        }

        public RestApiCallNonSuccessException(HttpStatusCode statusCode, string reasonPhrase, string message, Exception inner)
            : base(message, inner)
        {
            StatusCode = statusCode;
            ReasonPhrase = reasonPhrase;
        }
    }
}

