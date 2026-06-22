using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexivraChatBackend.Models;
using NexivraChatBackend.Repositories;

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
        public IActionResult GetRooms()
        {
            var rooms = _roomRepository.GetAll();
            return Ok(rooms);
        }

        [HttpPost]
        public IActionResult CreateRoom([FromBody] CreateRoomDto dto)
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

            _roomRepository.Create(room);
            return Ok(room);
        }

        [HttpGet("{id}/messages")]
        public IActionResult GetRoomMessages(int id, [FromQuery] int limit = 50, [FromQuery] int offset = 0)
        {
            var room = _roomRepository.GetById(id);
            if (room == null)
            {
                return NotFound("Phòng chat không tồn tại.");
            }

            var messages = _messageRepository.GetMessagesByRoom(id, limit, offset);
            return Ok(messages);
        }
    }

    public class CreateRoomDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
