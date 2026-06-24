using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace NexivraChatBackend.Services
{
    public class AiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public AiService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            var configKey = config["Gemini:ApiKey"];
            var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            _apiKey = !string.IsNullOrEmpty(configKey) ? configKey : (!string.IsNullOrEmpty(envKey) ? envKey : string.Empty);
        }

        public async IAsyncEnumerable<string> StreamResponseAsync(string prompt, string conversationHistory = "")
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                // MOCK MODE: Trả về chữ chạy từng từ nếu không có API key
                var mockText = $"[DEMO MODE] Chào bạn! Tôi là AI Copilot hỗ trợ kênh chat của bạn. Bạn vừa gửi yêu cầu: \"{prompt}\". Hiện tại hệ thống chưa cấu hình Gemini API Key trong appsettings.json, đây là phản hồi mô phỏng streaming từ backend qua SignalR Hub.";
                var words = mockText.Split(' ');
                foreach (var word in words)
                {
                    yield return word + " ";
                    await Task.Delay(120);
                }
                yield break;
            }

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:streamGenerateContent?alt=sse&key={_apiKey}";
            
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = $"{conversationHistory}\nUser: {prompt}\nAI Copilot:" }
                        }
                    }
                },
                systemInstruction = new
                {
                    parts = new[]
                    {
                        new { text = "Bạn là một trợ lý AI Copilot thông minh, tích hợp trong phòng chat realtime. Bạn đồng hành và hỗ trợ nhóm người dùng. Trả lời ngắn gọn, súc tích, mang tính xây dựng và thân thiện. Hãy viết câu trả lời bằng tiếng Việt." }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };

            using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
            {
                if (!response.IsSuccessStatusCode)
                {
                    yield return $"[Lỗi kết nối Gemini API: {response.StatusCode}]";
                    yield break;
                }

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream))
                {
                    // Định dạng SSE (alt=sse): mỗi sự kiện là một dòng "data: {json hoàn chỉnh}",
                    // các sự kiện cách nhau bằng dòng trống. Mỗi payload là một JSON parse được ngay.
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        if (!line.StartsWith("data:")) continue;

                        var payload = line.Substring("data:".Length).Trim();
                        if (string.IsNullOrEmpty(payload) || payload == "[DONE]") continue;

                        string? textChunk = null;
                        try
                        {
                            using (var doc = JsonDocument.Parse(payload))
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
                                    textChunk = textProp.GetString();
                                }
                            }
                        }
                        catch
                        {
                            // Bỏ qua chunk JSON không hợp lệ (hiếm gặp trong SSE)
                        }

                        if (!string.IsNullOrEmpty(textChunk))
                        {
                            yield return textChunk;
                        }
                    }
                }
            }
        }

        public async Task<string> GenerateContentAsync(string prompt, string systemInstruction = "")
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                return string.Empty;
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
                            new { text = prompt }
                        }
                    }
                },
                systemInstruction = !string.IsNullOrEmpty(systemInstruction) ? new
                {
                    parts = new[]
                    {
                        new { text = systemInstruction }
                    }
                } : null
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    return string.Empty;
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
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
