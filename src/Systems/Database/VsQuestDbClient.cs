#nullable enable
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace VsQuest.Systems.Database
{
    /// <summary>
    /// Result of an API call to vsquest database API.
    /// </summary>
    public class VsQuestApiResponse
    {
        public bool IsSuccess { get; init; }
        public int StatusCode { get; init; }
        public string Content { get; init; } = "";
        public string? ErrorMessage { get; init; }

        public T? Deserialize<T>() where T : class
        {
            if (!IsSuccess || string.IsNullOrWhiteSpace(Content))
                return null;

            try
            {
                return JsonConvert.DeserializeObject<T>(Content);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// HTTP client wrapper for communicating with the vsquest database API.
    /// Handles authentication headers, JSON serialization, timeouts, and error parsing.
    /// </summary>
    public class VsQuestDbClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly bool _enabled;

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None,
        };

        public VsQuestDbClient(VsQuestDbConfig config)
        {
            _baseUrl = config.BaseUrl.TrimEnd('/');
            _apiKey = config.ApiKey;
            _enabled = config.EnableSync;

            if (!_enabled)
            {
                _httpClient = new HttpClient();
                return;
            }

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds),
            };

            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
            }
        }

        public bool IsEnabled => _enabled;

        private string BuildUrl(string endpoint)
        {
            return $"{_baseUrl}/{endpoint.TrimStart('/')}";
        }

        public async Task<VsQuestApiResponse> GetAsync(string endpoint)
            => await SendAsync(HttpMethod.Get, endpoint, null);

        public async Task<VsQuestApiResponse> PostAsync(string endpoint, object? body = null)
            => await SendAsync(HttpMethod.Post, endpoint, body);

        public async Task<VsQuestApiResponse> PutAsync(string endpoint, object? body = null)
            => await SendAsync(HttpMethod.Put, endpoint, body);

        public async Task<VsQuestApiResponse> PatchAsync(string endpoint, object? body = null)
            => await SendAsync(new HttpMethod("PATCH"), endpoint, body);

        private async Task<VsQuestApiResponse> SendAsync(HttpMethod method, string endpoint, object? body)
        {
            if (!_enabled)
            {
                return new VsQuestApiResponse
                {
                    IsSuccess = false,
                    StatusCode = 0,
                    ErrorMessage = "VsQuest DB sync is disabled",
                };
            }

            try
            {
                var request = new HttpRequestMessage(method, BuildUrl(endpoint));

                if (body != null)
                {
                    var json = JsonConvert.SerializeObject(body, JsonSettings);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                return new VsQuestApiResponse
                {
                    IsSuccess = response.IsSuccessStatusCode,
                    StatusCode = (int)response.StatusCode,
                    Content = content,
                };
            }
            catch (TaskCanceledException)
            {
                return new VsQuestApiResponse
                {
                    IsSuccess = false,
                    StatusCode = 0,
                    ErrorMessage = "Request timed out.",
                };
            }
            catch (Exception ex)
            {
                return new VsQuestApiResponse
                {
                    IsSuccess = false,
                    StatusCode = 0,
                    ErrorMessage = ex.Message,
                };
            }
        }

        public async Task<bool> IsHealthyAsync()
        {
            if (!_enabled) return false;
            var response = await GetAsync("/status");
            return response.IsSuccess;
        }
    }
}
