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

        public UsersController(
            UserRepository userRepository, 
            PrivateChatRepository privateChatRepository,
            MessageRepository messageRepository)
        {
            _userRepository = userRepository;
            _privateChatRepository = privateChatRepository;
            _messageRepository = messageRepository;
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
        public IActionResult GetOrCreatePrivateChat([FromBody] CreatePrivateChatDto dto)
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

            var privateChat = _privateChatRepository.GetOrCreate(currentUserId, dto.ReceiverId);
            return Ok(privateChat);
        }

        [HttpGet("private-chat/{id}/messages")]
        public async Task<IActionResult> GetPrivateChatMessages(int id, [FromQuery] int limit = 50, [FromQuery] int offset = 0)
        {
            var chat = _privateChatRepository.GetById(id);
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

            var messages = await _messageRepository.GetMessagesByPrivateChat(id, limit, offset);
            return Ok(messages);
        }
    }

    public class CreatePrivateChatDto
    {
        public int ReceiverId { get; set; }
    }
}
