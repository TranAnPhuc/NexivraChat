using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexivraChatBackend.Models;
using NexivraChatBackend.Repositories;
using System.Threading.Tasks;

namespace NexivraChatBackend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class RoomsController : ControllerBase
    {
        private readonly RoomRepository _roomRepository;
        private readonly MessageRepository _messageRepository;

        public RoomsController(RoomRepository roomRepository, MessageRepository messageRepository)
        {
            _roomRepository = roomRepository;
            _messageRepository = messageRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetRooms()
        {
            var rooms = await _roomRepository.GetAll();
            return Ok(rooms);
        }

        [HttpPost]
        public async Task<IActionResult> CreateRoom([FromBody] CreateRoomDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest("Tên phòng không được để trống.");
            }

            var room = new ChatRoom
            {
                Name = dto.Name,
                Description = dto.Description
            };

            await _roomRepository.Create(room);
            return Ok(room);
        }

        [HttpGet("{id}/messages")]
        public async Task<IActionResult> GetRoomMessages(int id, [FromQuery] int limit = 50, [FromQuery] int? beforeId = null, [FromQuery] int? afterId = null)
        {
            var room = await _roomRepository.GetById(id);
            if (room == null)
            {
                return NotFound("Phòng chat không tồn tại.");
            }

            var messages = await _messageRepository.GetMessagesByRoom(id, limit, beforeId, afterId);
            return Ok(messages);
        }

        [HttpGet("{id}/messages/search")]
        public async Task<IActionResult> SearchRoomMessages(int id, [FromQuery] string q, [FromQuery] int limit = 30, [FromQuery] int? beforeId = null)
        {
            var room = await _roomRepository.GetById(id);
            if (room == null)
            {
                return NotFound("Phòng chat không tồn tại.");
            }

            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            {
                return Ok(new System.Collections.Generic.List<Message>());
            }

            var results = await _messageRepository.SearchRoomMessages(id, q.Trim(), limit, beforeId);
            return Ok(results);
        }
    }

    public class CreateRoomDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
