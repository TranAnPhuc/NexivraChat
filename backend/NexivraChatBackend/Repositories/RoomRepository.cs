using Dapper;
using System.Collections.Generic;
using System.Data;
using NexivraChatBackend.Data;
using NexivraChatBackend.Models;

namespace NexivraChatBackend.Repositories
{
    public class RoomRepository
    {
        private readonly DapperContext _context;

        public RoomRepository(DapperContext context)
        {
            _context = context;
        }

        public List<ChatRoom> GetAll()
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT * FROM chat_rooms ORDER BY name ASC";
                return connection.Query<ChatRoom>(query).ToList();
            }
        }

        public ChatRoom? GetById(int id)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT * FROM chat_rooms WHERE id = @id LIMIT 1";
                return connection.QueryFirstOrDefault<ChatRoom>(query, new { id });
            }
        }

        public void Create(ChatRoom room)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    INSERT INTO chat_rooms (name, description, created_at) 
                    VALUES (@name, @description, @created_at) 
                    RETURNING id;";
                
                var id = connection.ExecuteScalar<int>(query, new 
                { 
                    name = room.Name, 
                    description = room.Description,
                    created_at = DateTime.Now
                });
                
                room.Id = id;
            }
        }
    }
}