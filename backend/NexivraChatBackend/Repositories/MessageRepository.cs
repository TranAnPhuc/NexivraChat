using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NexivraChatBackend.Data;
using NexivraChatBackend.Models;

namespace NexivraChatBackend.Repositories
{
    public class MessageRepository
    {
        private readonly DapperContext _context;

        public MessageRepository(DapperContext context)
        {
            _context = context;
        }

        public List<Message> GetOldMessages(int limit, int offset)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT * FROM messages ORDER BY created_at DESC LIMIT @limit OFFSET @offset";
                return connection.Query<Message>(query, new { limit, offset }).ToList();
            }
        }

        public List<Message> GetMessagesByRoom(int roomId, int limit = 50, int offset = 0)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT * FROM messages WHERE room_id = @roomId ORDER BY created_at DESC LIMIT @limit OFFSET @offset";
                var result = connection.Query<Message>(query, new { roomId, limit, offset }).ToList();
                result.Reverse(); // Trả về theo thứ tự thời gian tăng dần
                return result;
            }
        }

        public void SaveNewMessage(Message message)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    INSERT INTO messages (room_id, sender_name, content, created_at, is_ai) 
                    VALUES (@room_id, @sender_name, @content, @created_at, @is_ai)
                    RETURNING id;";
                
                var id = connection.ExecuteScalar<int>(query, new 
                { 
                    room_id = message.RoomId,
                    sender_name = message.SenderName,
                    content = message.Content, 
                    created_at = message.CreatedAt == default ? DateTime.Now : message.CreatedAt,
                    is_ai = message.IsAi
                });
                message.Id = id;
            }
        }
    }
}
