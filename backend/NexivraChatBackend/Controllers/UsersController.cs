using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexivraChatBackend.Models;
using NexivraChatBackend.Repositories;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NexivraChatBackend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly UserRepository _userRepository;
        private readonly PrivateChatRepository _privateChatRepository;
        private readonly MessageRepository _messageRepository;
        private readonly ConversationReadRepository _conversationReadRepository;
        private readonly MentionRepository _mentionRepository;

        public UsersController(
            UserRepository userRepository,
            PrivateChatRepository privateChatRepository,
            MessageRepository messageRepository,
            ConversationReadRepository conversationReadRepository,
            MentionRepository mentionRepository)
        {
            _userRepository = userRepository;
            _privateChatRepository = privateChatRepository;
            _messageRepository = messageRepository;
            _conversationReadRepository = conversationReadRepository;
            _mentionRepository = mentionRepository;
        }

        // Số tin chưa đọc theo phòng và theo DM cho user hiện tại.
        [HttpGet("unread-counts")]
        public async Task<IActionResult> GetUnreadCounts()
        {
            var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserIdStr) || !int.TryParse(currentUserIdStr, out var currentUserId))
            {
                return Unauthorized();
            }

            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }

            var counts = await _conversationReadRepository.GetUnreadCounts(currentUserId, username);
            return Ok(counts);
        }

        [HttpGet("mentions")]
        public async Task<IActionResult> GetUnreadMentions()
        {
            var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserIdStr) || !int.TryParse(currentUserIdStr, out var currentUserId))
            {
                return Unauthorized();
            }

            var roomIds = await _mentionRepository.GetUnreadMentionRoomIds(currentUserId);
            return Ok(roomIds);
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserIdStr) || !int.TryParse(currentUserIdStr, out var currentUserId))
            {
                return Unauthorized();
            }

            var allUsers = await _userRepository.GetAll();

            // Lọc bỏ user hiện tại và chỉ lấy Id + Username
            var otherUsers = allUsers
                .Where(u => u.Id != currentUserId)
                .Select(u => new { u.Id, u.Username })
                .ToList();

            return Ok(otherUsers);
        }

        [HttpPost("private-chat")]
        public async Task<IActionResult> GetOrCreatePrivateChat([FromBody] CreatePrivateChatDto dto)
        {
            var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserIdStr) || !int.TryParse(currentUserIdStr, out var currentUserId))
            {
                return Unauthorized();
            }

            if (dto.ReceiverId <= 0)
            {
                return BadRequest("Receiver ID không hợp lệ.");
            }

            var privateChat = await _privateChatRepository.GetOrCreate(currentUserId, dto.ReceiverId);
            return Ok(privateChat);
        }

        [HttpGet("private-chat/{id}/messages")]
        public async Task<IActionResult> GetPrivateChatMessages(int id, [FromQuery] int limit = 50, [FromQuery] int? beforeId = null, [FromQuery] int? afterId = null)
        {
            var chat = await _privateChatRepository.GetById(id);
            if (chat == null)
            {
                return NotFound("Cuộc hội thoại không tồn tại.");
            }

            var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserIdStr) || !int.TryParse(currentUserIdStr, out var currentUserId))
            {
                return Unauthorized();
            }

            // Đảm bảo user hiện tại là thành viên của cuộc hội thoại này
            if (chat.User1Id != currentUserId && chat.User2Id != currentUserId)
            {
                return Forbid();
            }

            var messages = await _messageRepository.GetMessagesByPrivateChat(id, limit, beforeId, afterId);

            var partnerUserId = chat.User1Id == currentUserId ? chat.User2Id : chat.User1Id;
            var partnerLastReadId = await _conversationReadRepository.GetPartnerLastReadMessageId(currentUserId, partnerUserId);
            Response.Headers.Append("X-Partner-Last-Read-Id", partnerLastReadId.ToString());

            return Ok(messages);
        }

        [HttpGet("private-chat/{id}/messages/search")]
        public async Task<IActionResult> SearchPrivateChatMessages(int id, [FromQuery] string q, [FromQuery] int limit = 30, [FromQuery] int? beforeId = null)
        {
            var chat = await _privateChatRepository.GetById(id);
            if (chat == null)
            {
                return NotFound("Cuộc hội thoại không tồn tại.");
            }

            var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserIdStr) || !int.TryParse(currentUserIdStr, out var currentUserId))
            {
                return Unauthorized();
            }

            if (chat.User1Id != currentUserId && chat.User2Id != currentUserId)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            {
                return Ok(new System.Collections.Generic.List<Message>());
            }

            var results = await _messageRepository.SearchPrivateChatMessages(id, q.Trim(), limit, beforeId);
            return Ok(results);
        }
    }

    public class CreatePrivateChatDto
    {
        public int ReceiverId { get; set; }
    }
}
