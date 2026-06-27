using System.Configuration;

namespace SyncAgent.Configuration
{
    /// <summary>Strongly-typed access to the four App.config settings.</summary>
    public class AppSettings
    {
        /// <summary>Base URL of the sync platform API.</summary>
        public string PlatformBaseUrl { get; }

        /// <summary>API key sent in the X-Api-Key header.</summary>
        public string PlatformApiKey { get; }

        /// <summary>Connection string to the AdventureWorks2025 database.</summary>
        public string DatabaseConnectionString { get; }

        /// <summary>Seconds to wait between polls when the queue is empty.</summary>
        public int PollingIntervalSeconds { get; }

        public AppSettings(string platformBaseUrl, string platformApiKey, string databaseConnectionString, int pollingIntervalSeconds)
        {
            PlatformBaseUrl = platformBaseUrl;
            PlatformApiKey = platformApiKey;
            DatabaseConnectionString = databaseConnectionString;
            PollingIntervalSeconds = pollingIntervalSeconds;
        }

        /// <summary>Loads and validates the settings from App.config appSettings.</summary>
        public static AppSettings Load()
        {
            return new AppSettings(
                Require("PlatformBaseUrl"),
                Require("PlatformApiKey"),
                Require("DatabaseConnectionString"),
                int.Parse(Require("PollingIntervalSeconds")));
        }

        private static string Require(string key)
        {
            var value = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(value))
                throw new ConfigurationErrorsException("Missing required App.config appSettings key: " + key);
            return value;
        }
    }
}
