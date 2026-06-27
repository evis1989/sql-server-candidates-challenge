using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SyncAgent.Http;

namespace SyncAgent.Tests.Http
{
    [TestFixture]
    public class SyncPlatformClientTests
    {
        [Test]
        public void GetNextTask_returns_null_on_204()
        {
            var client = ClientReturning(new HttpResponseMessage(HttpStatusCode.NoContent));

            var task = client.GetNextTask();

            Assert.That(task, Is.Null);
        }

        [Test]
        public void GetNextTask_deserializes_task_on_200()
        {
            const string body =
                "{\"taskId\":\"01ABC\",\"taskType\":\"GetCustomers\"," +
                "\"parameters\":{\"modifiedSince\":\"2025-01-01T00:00:00Z\"}," +
                "\"createdAt\":\"2026-03-12T10:30:00Z\"}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };
            var client = ClientReturning(response);

            var task = client.GetNextTask();

            Assert.That(task, Is.Not.Null);
            Assert.That(task.TaskId, Is.EqualTo("01ABC"));
            Assert.That(task.TaskType, Is.EqualTo("GetCustomers"));
            Assert.That(task.Parameters.ModifiedSince,
                Is.EqualTo(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        }

        private static SyncPlatformClient ClientReturning(HttpResponseMessage response)
        {
            var http = new HttpClient(new StubHandler(response))
            {
                BaseAddress = new Uri("http://localhost:5100/")
            };
            return new SyncPlatformClient(http);
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage _response;
            public StubHandler(HttpResponseMessage response) { _response = response; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_response);
            }
        }
    }
}
