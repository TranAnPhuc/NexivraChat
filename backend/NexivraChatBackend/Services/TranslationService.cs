using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NexivraChatBackend.Services
{
    public class TranslationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TranslationService> _logger;
        private readonly string _apiKey;

        public TranslationService(HttpClient httpClient, IConfiguration config, ILogger<TranslationService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            var configKey = config["Gemini:ApiKey"] ?? config["Gemini__ApiKey"];
            var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            _apiKey = !string.IsNullOrEmpty(configKey) ? configKey : (!string.IsNullOrEmpty(envKey) ? envKey : string.Empty);
        }

        public async Task<string> TranslateTextAsync(string text, string targetLanguage)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                // MOCK MODE: dịch mô phỏng nếu không có API key
                return $"[MOCK TRANSLATION to {targetLanguage}]: {text}";
            }

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = $"Translate the following text into {targetLanguage}. Respond ONLY with the translation, do not add any explanation, notes, or extra characters.\n\nText to translate:\n{text}" }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Lỗi kết nối Gemini Translation API: {StatusCode}", response.StatusCode);
                    return $"[Lỗi Gemini API: {response.StatusCode}]";
                }

                var responseString = await response.Content.ReadAsStringAsync();
                using (var doc = JsonDocument.Parse(responseString))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("candidates", out var candidates) &&
                        candidates.ValueKind == JsonValueKind.Array &&
                        candidates.GetArrayLength() > 0 &&
                        candidates[0].TryGetProperty("content", out var contentElement) &&
                        contentElement.TryGetProperty("parts", out var parts) &&
                        parts.ValueKind == JsonValueKind.Array &&
                        parts.GetArrayLength() > 0 &&
                        parts[0].TryGetProperty("text", out var textProp))
                    {
                        return textProp.GetString()?.Trim() ?? string.Empty;
                    }
                }

                return "[Lỗi: Định dạng phản hồi không hợp lệ]";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ngoại lệ khi gọi Gemini Translation API");
                return $"[Lỗi kết nối: {ex.Message}]";
            }
        }
    }
}
