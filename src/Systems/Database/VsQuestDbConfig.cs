using Newtonsoft.Json;

namespace VsQuest.Systems.Database
{
    /// <summary>
    /// Configuration for VsQuest database API connection.
    /// Loaded from JSON config file by the Vintage Story mod config system.
    /// </summary>
    public class VsQuestDbConfig
    {
        /// <summary>
        /// Base URL of the alegacy_db API (e.g. "http://localhost:8080").
        /// </summary>
        [JsonProperty("baseUrl")]
        public string BaseUrl { get; set; } = "http://localhost:8080";

        /// <summary>
        /// API key sent as X-Api-Key header for authentication.
        /// </summary>
        [JsonProperty("apiKey")]
        public string ApiKey { get; set; } = "";

        /// <summary>
        /// Timeout in seconds for HTTP requests.
        /// </summary>
        [JsonProperty("timeoutSeconds")]
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Whether to enable MySQL sync for quest progress.
        /// </summary>
        [JsonProperty("enableSync")]
        public bool EnableSync { get; set; } = true;

        /// <summary>
        /// Debounce interval in seconds for batch writes (default: 30s).
        /// </summary>
        [JsonProperty("debounceSeconds")]
        public int DebounceSeconds { get; set; } = 30;
    }
}
