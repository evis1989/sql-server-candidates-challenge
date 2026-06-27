using System;
using System.Net;

namespace SyncAgent.Http
{
    /// <summary>
    /// Thrown when the platform returns a non-success HTTP status. Carries the
    /// status code so callers can distinguish retryable (5xx) from non-retryable
    /// (4xx) responses — HttpRequestException does not carry it on .NET Framework.
    /// </summary>
    public class PlatformResponseException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public PlatformResponseException(HttpStatusCode statusCode, string message)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
