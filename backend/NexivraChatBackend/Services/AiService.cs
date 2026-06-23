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

        public AiService(IConfiguration config)
        {
            _httpClient = new HttpClient();
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

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:streamGenerateContent?key={_apiKey}";
            
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
                    while (!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrEmpty(line)) continue;

                        var cleanLine = line.Trim();
                        // Dọn dẹp JSON stream format từ Gemini
                        if (cleanLine.StartsWith("[")) cleanLine = cleanLine.Substring(1);
                        if (cleanLine.EndsWith("]")) cleanLine = cleanLine.Substring(0, cleanLine.Length - 1);
                        if (cleanLine.StartsWith(",")) cleanLine = cleanLine.Substring(1);
                        cleanLine = cleanLine.Trim();

                        if (string.IsNullOrEmpty(cleanLine)) continue;

                        string? textChunk = null;
                        try
                        {
                            using (var doc = JsonDocument.Parse(cleanLine))
                            {
                                var root = doc.RootElement;
                                if (root.TryGetProperty("candidates", out var candidates) && 
                                    candidates.ValueKind == JsonValueKind.Array && 
                                    candidates.GetArrayLength() > 0)
                                {
                                    var contentElement = candidates[0].GetProperty("content");
                                    var parts = contentElement.GetProperty("parts");
                                    if (parts.ValueKind == JsonValueKind.Array && parts.GetArrayLength() > 0)
                                    {
                                        textChunk = parts[0].GetProperty("text").GetString();
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Bỏ qua các dòng format json ko hoàn chỉnh trong stream
                        }

                        if (!string.IsNullOrEmpty(textChunk))
                        {
                            yield return textChunk;
                        }
                    }
                }
            }
        }
    }
}
