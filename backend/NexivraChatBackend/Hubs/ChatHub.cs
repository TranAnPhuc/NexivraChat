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
        private readonly PresenceTracker _presenceTracker;
        private readonly PrivateChatRepository _privateChatRepository;
        private readonly ConversationReadRepository _conversationReadRepository;
        private readonly ReactionRepository _reactionRepository;
        private readonly UserRepository _userRepository;
        private readonly MentionRepository _mentionRepository;

        public ChatHub(
            MessageRepository messageRepository,
            AiService aiService,
            PresenceTracker presenceTracker,
            PrivateChatRepository privateChatRepository,
            ConversationReadRepository conversationReadRepository,
            ReactionRepository reactionRepository,
            UserRepository userRepository,
            MentionRepository mentionRepository)
        {
            _messageRepository = messageRepository;
            _aiService = aiService;
            _presenceTracker = presenceTracker;
            _privateChatRepository = privateChatRepository;
            _conversationReadRepository = conversationReadRepository;
            _reactionRepository = reactionRepository;
            _userRepository = userRepository;
            _mentionRepository = mentionRepository;
        }

        public async Task JoinRoom(int roomId)
        {
            var roomString = roomId.ToString();
            await Groups.AddToGroupAsync(Context.ConnectionId, roomString);

            var username = Context.User?.Identity?.Name ?? "Ẩn danh";
            _presenceTracker.UserJoined(roomId, Context.ConnectionId, username);

            await Clients.Group(roomString).SendAsync("ReceiveNotification", $"{username} đã tham gia phòng.");
            await Clients.Group(roomString).SendAsync("PresenceUpdate", roomId, _presenceTracker.GetOnlineUsers(roomId));
        }

        public async Task LeaveRoom(int roomId)
        {
            var roomString = roomId.ToString();
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomString);

            var username = Context.User?.Identity?.Name ?? "Ẩn danh";
            _presenceTracker.UserLeft(roomId, Context.ConnectionId, username);

            await Clients.Group(roomString).SendAsync("ReceiveNotification", $"{username} đã rời phòng.");
            await Clients.Group(roomString).SendAsync("PresenceUpdate", roomId, _presenceTracker.GetOnlineUsers(roomId));
            // Đảm bảo người khác không thấy "đang gõ" treo lại sau khi rời phòng
            await Clients.OthersInGroup(roomString).SendAsync("TypingUpdate", roomId, username, false);
        }

        public async Task Typing(int roomId, bool isTyping)
        {
            var username = Context.User?.Identity?.Name ?? "Ẩn danh";
            var roomString = roomId.ToString();
            // Chỉ gửi cho người khác trong phòng, không gửi lại cho chính người gõ
            await Clients.OthersInGroup(roomString).SendAsync("TypingUpdate", roomId, username, isTyping);
        }

        public override async Task OnConnectedAsync()
        {
            var username = Context.User?.Identity?.Name ?? "Ẩn danh";
            _presenceTracker.AddGlobalConnection(Context.ConnectionId, username);
            await Clients.All.SendAsync("GlobalPresenceUpdate", _presenceTracker.GetGlobalOnlineUsers());
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var username = Context.User?.Identity?.Name ?? "Ẩn danh";
            _presenceTracker.RemoveGlobalConnection(Context.ConnectionId);
            await Clients.All.SendAsync("GlobalPresenceUpdate", _presenceTracker.GetGlobalOnlineUsers());

            var affectedRooms = _presenceTracker.RemoveConnection(Context.ConnectionId);
            foreach (var roomId in affectedRooms)
            {
                var roomString = roomId.ToString();
                await Clients.Group(roomString).SendAsync("PresenceUpdate", roomId, _presenceTracker.GetOnlineUsers(roomId));
                await Clients.OthersInGroup(roomString).SendAsync("TypingUpdate", roomId, username, false);
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(int roomId, string content, int? replyToId = null, string? attachmentUrl = null, string? attachmentName = null, string? attachmentType = null, long? attachmentSize = null)
        {
            var username = Context.User?.Identity?.Name ?? "Ẩn danh";
            var roomString = roomId.ToString();

            int? senderId = null;
            if (int.TryParse(Context.UserIdentifier, out var parsedSenderId))
            {
                senderId = parsedSenderId;
            }

            int? validReplyToId = (replyToId.HasValue && replyToId.Value > 0) ? replyToId.Value : null;

            // 1. Lưu tin nhắn người dùng gửi vào Database
            var userMessage = new Message
            {
                RoomId = roomId,
                SenderId = senderId,
                SenderName = username,
                Content = content,
                CreatedAt = DateTime.Now,
                IsAi = false,
                ReplyToId = validReplyToId,
                AttachmentUrl = attachmentUrl,
                AttachmentName = attachmentName,
                AttachmentType = attachmentType,
                AttachmentSize = attachmentSize
            };
            await _messageRepository.SaveNewMessage(userMessage);

            if (validReplyToId.HasValue)
            {
                var orig = await _messageRepository.GetById(validReplyToId.Value);
                if (orig != null)
                {
                    userMessage.ReplyToSenderName = orig.SenderName;
                    userMessage.ReplyToContent = string.IsNullOrEmpty(orig.Content) ? "" : orig.Content.Substring(0, Math.Min(120, orig.Content.Length));
                }
            }

            // 2. Phát tin nhắn người dùng tới toàn phòng chat
            await Clients.Group(roomString).SendAsync("ReceiveMessage", userMessage);

            // 2b. Tín hiệu unread nhẹ tới MỌI user
            await Clients.All.SendAsync("UnreadUpdate", new { type = "room", id = roomId });

            // 2c. Xử lý @mention nhắc tên người dùng trong phòng
            var matches = System.Text.RegularExpressions.Regex.Matches(content, @"@([A-Za-z0-9_]+)");
            if (matches.Count > 0)
            {
                var mentionedUsernames = matches
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(m => m.Groups[1].Value)
                    .Where(u => !u.Equals(username, StringComparison.OrdinalIgnoreCase) && !u.Equals("copilot", StringComparison.OrdinalIgnoreCase))
                    .Distinct()
                    .ToList();

                if (mentionedUsernames.Any())
                {
                    var mentionedUsers = await _userRepository.GetByUsernames(mentionedUsernames);
                    if (mentionedUsers.Any())
                    {
                        var targetUserIds = mentionedUsers.Select(u => u.Id).ToList();
                        await _mentionRepository.SaveMentions(userMessage.Id, targetUserIds);

                        foreach (var targetUser in mentionedUsers)
                        {
                            await Clients.User(targetUser.Id.ToString()).SendAsync("MentionUpdate", new
                            {
                                roomId,
                                messageId = userMessage.Id,
                                fromUsername = username
                            });
                        }
                    }
                }
            }

            // 3. Xử lý gọi trợ lý AI nếu tin nhắn bắt đầu bằng tag @copilot
            if (content.Trim().StartsWith("@copilot", StringComparison.OrdinalIgnoreCase))
            {
                var queryText = content.Replace("@copilot", "", StringComparison.OrdinalIgnoreCase).Trim();
                if (string.IsNullOrEmpty(queryText))
                {
                    queryText = "Hãy giới thiệu ngắn gọn về bản thân bạn.";
                }

                // Lấy 10 tin nhắn gần nhất làm ngữ cảnh hội thoại
                var recentMessages = await _messageRepository.GetMessagesByRoom(roomId, 10, null);
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
                var tempAiMessageId = TempMessageId.Next();
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
                    await _messageRepository.SaveNewMessage(finalAiMessage);

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

        public async Task SendPrivateMessage(int receiverId, string content, int? replyToId = null, string? attachmentUrl = null, string? attachmentName = null, string? attachmentType = null, long? attachmentSize = null)
        {
            var senderIdStr = Context.UserIdentifier;
            if (string.IsNullOrEmpty(senderIdStr) || !int.TryParse(senderIdStr, out var senderId))
            {
                throw new HubException("Không xác định được danh tính người gửi.");
            }

            var username = Context.User?.Identity?.Name ?? "Ẩn danh";
            int? validReplyToId = (replyToId.HasValue && replyToId.Value > 0) ? replyToId.Value : null;

            // 1. Lấy hoặc tạo phòng chat 1-1
            var privateChat = await _privateChatRepository.GetOrCreate(senderId, receiverId);

            // 2. Lưu tin nhắn vào Database
            var userMessage = new Message
            {
                PrivateChatId = privateChat.Id,
                SenderId = senderId,
                SenderName = username,
                Content = content,
                CreatedAt = DateTime.Now,
                IsAi = false,
                ReplyToId = validReplyToId,
                AttachmentUrl = attachmentUrl,
                AttachmentName = attachmentName,
                AttachmentType = attachmentType,
                AttachmentSize = attachmentSize
            };
            await _messageRepository.SaveNewMessage(userMessage);

            if (validReplyToId.HasValue)
            {
                var orig = await _messageRepository.GetById(validReplyToId.Value);
                if (orig != null)
                {
                    userMessage.ReplyToSenderName = orig.SenderName;
                    userMessage.ReplyToContent = string.IsNullOrEmpty(orig.Content) ? "" : orig.Content.Substring(0, Math.Min(120, orig.Content.Length));
                }
            }

            // 3. Phát tin nhắn riêng tư tới cả người gửi và người nhận
            await Clients.Users(senderId.ToString(), receiverId.ToString()).SendAsync("ReceivePrivateMessage", userMessage);

            //     người nhận) để khớp badge keyed-by-user ở sidebar.
            await Clients.User(receiverId.ToString()).SendAsync("UnreadUpdate", new { type = "private", id = senderId });
        }

        // Đánh dấu đã đọc một hội thoại tới lastReadMessageId (id tin cuối client đã render).
        // DM phải kiểm tra người gọi là thành viên (như UsersController.GetPrivateChatMessages).
        public async Task MarkRead(int? roomId, int? privateChatId, int lastReadMessageId)
        {
            var meStr = Context.UserIdentifier;
            if (string.IsNullOrEmpty(meStr) || !int.TryParse(meStr, out var me))
            {
                throw new HubException("Không xác định được danh tính người dùng.");
            }

            if (roomId.HasValue)
            {
                await _conversationReadRepository.MarkRoomRead(me, roomId.Value, lastReadMessageId);
                // Đồng bộ các tab khác của chính user (badge phòng về 0).
                await Clients.User(meStr).SendAsync("ReadUpdate", new { roomId = (int?)roomId.Value, privateChatUserId = (int?)null });
            }
            else if (privateChatId.HasValue)
            {
                var chat = await _privateChatRepository.GetById(privateChatId.Value);
                if (chat == null || (chat.User1Id != me && chat.User2Id != me))
                {
                    throw new HubException("Bạn không có quyền với hội thoại này.");
                }
                await _conversationReadRepository.MarkPrivateChatRead(me, privateChatId.Value, lastReadMessageId);
                // ReadUpdate keyed theo người đối thoại để khớp badge sidebar.
                var otherUserId = chat.User1Id == me ? chat.User2Id : chat.User1Id;
                await Clients.User(meStr).SendAsync("ReadUpdate", new { roomId = (int?)null, privateChatUserId = (int?)otherUserId });
                // Gửi thông báo "SeenUpdate" tới người gửi để cập nhật trạng thái "Đã xem" thời gian thực.
                await Clients.User(otherUserId.ToString()).SendAsync("SeenUpdate", new { privateChatUserId = me, lastReadMessageId });
            }
            else
            {
                throw new HubException("Cần roomId hoặc privateChatId.");
            }
        }

        private static readonly string[] AllowedEmojis = new[] { "👍", "❤️", "😂", "😮", "😢", "🙏" };

        public async Task ToggleReaction(int messageId, string emoji)
        {
            if (messageId <= 0 || string.IsNullOrEmpty(emoji) || !AllowedEmojis.Contains(emoji))
            {
                throw new HubException("Emoji không hợp lệ.");
            }

            var userIdStr = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                throw new HubException("Không xác định được danh tính người dùng.");
            }

            var conv = await _reactionRepository.LookupConversation(messageId);
            if (conv == null)
            {
                throw new HubException("Không tìm thấy tin nhắn.");
            }

            if (conv.PrivateChatId.HasValue)
            {
                var chat = await _privateChatRepository.GetById(conv.PrivateChatId.Value);
                if (chat == null || (chat.User1Id != userId && chat.User2Id != userId))
                {
                    throw new HubException("Bạn không có quyền với hội thoại này.");
                }
            }

            var (reacted, count) = await _reactionRepository.ToggleReaction(messageId, userId, emoji);

            var payload = new { messageId, emoji, count, userId, reacted };

            if (conv.RoomId.HasValue)
            {
                await Clients.Group(conv.RoomId.Value.ToString()).SendAsync("ReactionUpdate", payload);
            }
            else if (conv.PrivateChatId.HasValue)
            {
                var chat = await _privateChatRepository.GetById(conv.PrivateChatId.Value);
                if (chat != null)
                {
                    await Clients.Users(chat.User1Id.ToString(), chat.User2Id.ToString()).SendAsync("ReactionUpdate", payload);
                }
            }
        }

        public async Task EditMessage(int messageId, string newContent)
        {
            if (messageId <= 0 || string.IsNullOrWhiteSpace(newContent))
            {
                throw new HubException("Dữ liệu chỉnh sửa không hợp lệ.");
            }

            var userIdStr = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                throw new HubException("Không xác định được danh tính người dùng.");
            }

            var affected = await _messageRepository.EditMessage(messageId, userId, newContent.Trim());
            if (affected == 0)
            {
                throw new HubException("Không có quyền hoặc tin không tồn tại.");
            }

            var conv = await _reactionRepository.LookupConversation(messageId);
            if (conv != null)
            {
                var payload = new { messageId, newContent = newContent.Trim(), editedAt = DateTime.Now };
                if (conv.RoomId.HasValue)
                {
                    await Clients.Group(conv.RoomId.Value.ToString()).SendAsync("MessageEdited", payload);
                }
                else if (conv.PrivateChatId.HasValue)
                {
                    var chat = await _privateChatRepository.GetById(conv.PrivateChatId.Value);
                    if (chat != null)
                    {
                        await Clients.Users(chat.User1Id.ToString(), chat.User2Id.ToString()).SendAsync("MessageEdited", payload);
                    }
                }
            }
        }

        public async Task DeleteMessage(int messageId)
        {
            if (messageId <= 0)
            {
                throw new HubException("Tin nhắn không hợp lệ.");
            }

            var userIdStr = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                throw new HubException("Không xác định được danh tính người dùng.");
            }

            var conv = await _reactionRepository.LookupConversation(messageId);
            var affected = await _messageRepository.SoftDeleteMessage(messageId, userId);
            if (affected == 0)
            {
                throw new HubException("Không có quyền hoặc tin không tồn tại.");
            }

            if (conv != null)
            {
                var payload = new { messageId };
                if (conv.RoomId.HasValue)
                {
                    await Clients.Group(conv.RoomId.Value.ToString()).SendAsync("MessageDeleted", payload);
                }
                else if (conv.PrivateChatId.HasValue)
                {
                    var chat = await _privateChatRepository.GetById(conv.PrivateChatId.Value);
                    if (chat != null)
                    {
                        await Clients.Users(chat.User1Id.ToString(), chat.User2Id.ToString()).SendAsync("MessageDeleted", payload);
                    }
                }
            }
        }
    }
}
