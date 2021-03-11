using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace BOG.DropZone
{
	/// <summary>
	/// Ensures that a payload too large exception sends 413--not a generic 500.
	/// </summary>
	public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="next"></param>
        public ExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// Invocation
        /// </summary>
        /// <param name="httpContext"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                HandleExceptionAsync(httpContext, ex);
            }
        }

        private static void HandleExceptionAsync(HttpContext context, Exception exception)
        {
            if (exception is Microsoft.AspNetCore.Http.BadHttpRequestException)
            {
                context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
            }
        }
    }
}
