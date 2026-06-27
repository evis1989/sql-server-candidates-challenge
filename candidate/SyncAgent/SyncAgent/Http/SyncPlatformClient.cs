using System;
using System.Net;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using SyncAgent.Configuration;
using SyncAgent.Models;

namespace SyncAgent.Http
{
    /// <summary>HttpClient-based implementation of the sync platform API.</summary>
    public class SyncPlatformClient : ISyncPlatformClient
    {
        private const string NextTaskPath = "api/sync/next-task";
        private const string ResultPath = "api/sync/result";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            // Always emit nulls (e.g. errorMessage) and normalize dates to UTC.
            NullValueHandling = NullValueHandling.Include,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc
        };

        private readonly HttpClient _http;

        /// <summary>Takes a configured HttpClient (base address + X-Api-Key already set).</summary>
        public SyncPlatformClient(HttpClient http)
        {
            _http = http;
        }

        /// <summary>Builds a client wired with the platform base URL and API key header.</summary>
        public static SyncPlatformClient Create(AppSettings settings)
        {
            var baseUrl = settings.PlatformBaseUrl.EndsWith("/")
                ? settings.PlatformBaseUrl
                : settings.PlatformBaseUrl + "/";
            var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
            http.DefaultRequestHeaders.Add("X-Api-Key", settings.PlatformApiKey);
            return new SyncPlatformClient(http);
        }

        public SyncTask GetNextTask()
        {
            using (var response = _http.GetAsync(NextTaskPath).GetAwaiter().GetResult())
            {
                if (response.StatusCode == HttpStatusCode.NoContent)
                    return null;

                response.EnsureSuccessStatusCode();
                var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonConvert.DeserializeObject<SyncTask>(body, JsonSettings);
            }
        }

        public void PostResult(SyncResult result)
        {
            var json = JsonConvert.SerializeObject(result, JsonSettings);
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var response = _http.PostAsync(ResultPath, content).GetAwaiter().GetResult())
            {
                // Surface the status code (4xx vs 5xx) so the retry policy can classify it.
                if (!response.IsSuccessStatusCode)
                    throw new PlatformResponseException(response.StatusCode,
                        "POST result returned " + (int)response.StatusCode + " " + response.ReasonPhrase + ".");
            }
        }
    }
}
