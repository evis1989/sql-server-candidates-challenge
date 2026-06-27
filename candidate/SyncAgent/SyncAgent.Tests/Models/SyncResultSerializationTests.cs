using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SyncAgent.Models;

namespace SyncAgent.Tests.Models
{
    [TestFixture]
    public class SyncResultSerializationTests
    {
        [Test]
        public void Completed_result_emits_explicit_null_errorMessage()
        {
            var result = SyncResult.Completed("t1", "GetCustomers", new object[] { new { id = 1 } });

            var json = JObject.Parse(JsonConvert.SerializeObject(result));

            Assert.That(json.ContainsKey("errorMessage"), Is.True, "errorMessage must always be present");
            Assert.That(json["errorMessage"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That(json["status"].Value<string>(), Is.EqualTo("completed"));
            Assert.That(json["recordCount"].Value<int>(), Is.EqualTo(1));
        }

        [Test]
        public void Failed_result_emits_error_message()
        {
            var result = SyncResult.Failed("t1", "GetCustomers", "boom");

            var json = JObject.Parse(JsonConvert.SerializeObject(result));

            Assert.That(json["errorMessage"].Value<string>(), Is.EqualTo("boom"));
            Assert.That(json["status"].Value<string>(), Is.EqualTo("failed"));
            Assert.That(json["data"].Type, Is.EqualTo(JTokenType.Null));
        }
    }
}
