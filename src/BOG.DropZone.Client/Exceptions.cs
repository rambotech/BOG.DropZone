using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace BOG.DropZone.Client
{
    public class RestApiNonSuccessException : Exception
    {
        public HttpStatusCode StatusCode { get; set; }

        public RestApiNonSuccessException(HttpStatusCode statusCode)
        {
            StatusCode = statusCode;
        }

        public RestApiNonSuccessException(HttpStatusCode statusCode, string message)
            : base(message)
        {
            StatusCode = statusCode;
        }

        public RestApiNonSuccessException(HttpStatusCode statusCode, string message, Exception inner)
            : base(message, inner)
        {
            StatusCode = statusCode;
        }
    }
}

