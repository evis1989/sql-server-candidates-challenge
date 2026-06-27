using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using Moq;
using NUnit.Framework;
using SyncAgent;
using SyncAgent.Http;
using SyncAgent.Models;

namespace SyncAgent.Tests
{
    [TestFixture]
    public class ResultPublisherTests
    {
        private static readonly TimeSpan[] NoWait = { TimeSpan.Zero };
        private static readonly TimeSpan[] NoWaitTwice = { TimeSpan.Zero, TimeSpan.Zero };

        private static SyncResult Result() => SyncResult.Completed("t1", "GetCustomers", new object[0]);

        [Test]
        public void Transient_failure_then_success_delivers()
        {
            var calls = 0;
            var client = new Mock<ISyncPlatformClient>();
            client.Setup(c => c.PostResult(It.IsAny<SyncResult>()))
                  .Callback(() => { if (++calls == 1) throw new HttpRequestException("transient"); });
            var publisher = new ResultPublisher(client.Object, new ManualResetEvent(false), NoWait);

            Assert.DoesNotThrow(() => publisher.Publish(Result()));

            client.Verify(c => c.PostResult(It.IsAny<SyncResult>()), Times.Exactly(2),
                "first attempt fails, second succeeds");
        }

        [Test]
        public void Permanent_5xx_exhausts_attempts_logs_and_does_not_throw()
        {
            var client = new Mock<ISyncPlatformClient>();
            client.Setup(c => c.PostResult(It.IsAny<SyncResult>()))
                  .Throws(new PlatformResponseException(HttpStatusCode.InternalServerError, "500"));
            var publisher = new ResultPublisher(client.Object, new ManualResetEvent(false), NoWaitTwice);

            var log = CaptureConsole(() => Assert.DoesNotThrow(() => publisher.Publish(Result())));

            client.Verify(c => c.PostResult(It.IsAny<SyncResult>()), Times.Exactly(3), "1 + 2 retries");
            Assert.That(log, Does.Contain("t1"));
            Assert.That(log, Does.Contain("3 attempts"));
        }

        [Test]
        public void Http_400_is_not_retried_and_fails_immediately()
        {
            var client = new Mock<ISyncPlatformClient>();
            client.Setup(c => c.PostResult(It.IsAny<SyncResult>()))
                  .Throws(new PlatformResponseException(HttpStatusCode.BadRequest, "400"));
            var publisher = new ResultPublisher(client.Object, new ManualResetEvent(false), NoWaitTwice);

            Assert.DoesNotThrow(() => publisher.Publish(Result()));

            client.Verify(c => c.PostResult(It.IsAny<SyncResult>()), Times.Once, "4xx must not be retried");
        }

        [Test]
        public void Stop_during_backoff_returns_without_waiting()
        {
            var stop = new ManualResetEvent(false);
            var client = new Mock<ISyncPlatformClient>();
            // First attempt fails transiently AND signals stop, so the backoff should abort at once.
            client.Setup(c => c.PostResult(It.IsAny<SyncResult>()))
                  .Callback(() => { stop.Set(); throw new HttpRequestException("transient"); });
            // Real 1s/2s backoffs: the test proves stop interrupts them, not that they are short.
            var publisher = new ResultPublisher(client.Object, stop);

            var sw = Stopwatch.StartNew();
            Assert.DoesNotThrow(() => publisher.Publish(Result()));
            sw.Stop();

            client.Verify(c => c.PostResult(It.IsAny<SyncResult>()), Times.Once, "stop ends retrying after attempt 1");
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(500), "must not wait out the 1s backoff");
        }

        private static string CaptureConsole(Action action)
        {
            var original = Console.Out;
            var buffer = new StringWriter();
            Console.SetOut(buffer);
            try { action(); }
            finally { Console.SetOut(original); }
            return buffer.ToString();
        }
    }
}
