using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NexivraChatBackend.Models;
using NexivraChatBackend.Repositories;
using NexivraChatBackend.Services;
using System;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace NexivraChatBackend.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly MessageRepository _messageRepository;
        private readonly AiService _aiService;

        public ChatHub(MessageRepository messageRepository, AiService aiService)
        {
            _messageRepository = messageRepository;
            _aiService = aiService;
        }

        public async Task JoinRoom(int roomId)
        {
            var roomString = roomId.ToString();
            await Groups.AddToGroupAsync(Context.ConnectionId, roomString);
            
            var username = Context.User?.Identity?.Name ?? "Ẩn danh";
            await Clients.Group(roomString).SendAsync("ReceiveNotification", $"{username} đã tham gia phòng.");
        }

        public async Task LeaveRoom(int roomId)
        {
            var roomString = roomId.ToString();
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomString);
            
            var username = Context.User?.Identity?.Name ?? "Ẩn danh";
            await Clients.Group(roomString).SendAsync("ReceiveNotification", $"{username} đã rời phòng.");
        }

        public async Task SendMessage(int roomId, string content)
        {
            var username = Context.User?.Identity?.Name ?? "Ẩn danh";
            var roomString = roomId.ToString();

            // 1. Lưu tin nhắn người dùng gửi vào Database
            var userMessage = new Message
            {
                RoomId = roomId,
                SenderName = username,
                Content = content,
                CreatedAt = DateTime.Now,
                IsAi = false
            };
            _messageRepository.SaveNewMessage(userMessage);

            // 2. Phát tin nhắn người dùng tới toàn phòng chat
            await Clients.Group(roomString).SendAsync("ReceiveMessage", userMessage);

            // 3. Xử lý gọi trợ lý AI nếu tin nhắn bắt đầu bằng tag @copilot
            if (content.Trim().StartsWith("@copilot", StringComparison.OrdinalIgnoreCase))
            {
                var queryText = content.Replace("@copilot", "", StringComparison.OrdinalIgnoreCase).Trim();
                if (string.IsNullOrEmpty(queryText))
                {
                    queryText = "Hãy giới thiệu ngắn gọn về bản thân bạn.";
                }

                // Lấy 10 tin nhắn gần nhất làm ngữ cảnh hội thoại
                var recentMessages = _messageRepository.GetMessagesByRoom(roomId, 10, 0);
                var conversationContext = new StringBuilder();
                foreach (var msg in recentMessages)
                {
                    if (msg.Content != content)
                    {
                        var role = msg.IsAi ? "AI Copilot" : msg.SenderName;
                        conversationContext.AppendLine($"{role}: {msg.Content}");
                    }
                }

                // 4. Tạo tin nhắn AI tạm thời (giá trị ID âm để tránh trùng lặp)
                var tempAiMessageId = -1 * new Random().Next(1, 1000000);
                var aiPlaceholder = new Message
                {
                    Id = tempAiMessageId,
                    RoomId = roomId,
                    SenderName = "AI Copilot",
                    Content = "",
                    CreatedAt = DateTime.Now,
                    IsAi = true
                };

                // Phát tín hiệu thông báo có tin nhắn mới từ AI để frontend chuẩn bị render
                await Clients.Group(roomString).SendAsync("ReceiveMessage", aiPlaceholder);

                // 5. Stream kết quả từ AI Service
                var compiledResponse = new StringBuilder();
                try
                {
                    await foreach (var token in _aiService.StreamResponseAsync(queryText, conversationContext.ToString()))
                    {
                        compiledResponse.Append(token);
                        // Truyền token thời gian thực về phòng chat
                        await Clients.Group(roomString).SendAsync("ReceiveAiToken", tempAiMessageId, token);
                    }

                    // 6. Lưu tin nhắn AI hoàn chỉnh vào database sau khi stream xong
                    var finalAiMessage = new Message
                    {
                        RoomId = roomId,
                        SenderName = "AI Copilot",
                        Content = compiledResponse.ToString(),
                        CreatedAt = DateTime.Now,
                        IsAi = true
                    };
                    _messageRepository.SaveNewMessage(finalAiMessage);

                    // 7. Thông báo hoàn thành và thay thế ID âm bằng ID chính thức từ DB
                    await Clients.Group(roomString).SendAsync("ReceiveAiComplete", tempAiMessageId, finalAiMessage.Id, finalAiMessage.Content);
                }
                catch (Exception ex)
                {
                    await Clients.Group(roomString).SendAsync("ReceiveAiToken", tempAiMessageId, $"\n[Lỗi kết nối AI: {ex.Message}]");
                    await Clients.Group(roomString).SendAsync("ReceiveAiComplete", tempAiMessageId, 0, $"Lỗi: {ex.Message}");
                }
            }
        }
    }
}
