using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public async Task<List<ChatRoom>> GetAll()
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT id, name, description FROM chat_rooms ORDER BY name ASC";
                return (await connection.QueryAsync<ChatRoom>(query)).ToList();
            }
        }

        public async Task<ChatRoom?> GetById(int id)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT id, name, description FROM chat_rooms WHERE id = @id LIMIT 1";
                return await connection.QueryFirstOrDefaultAsync<ChatRoom>(query, new { id });
            }
        }

        public async Task Create(ChatRoom room)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    INSERT INTO chat_rooms (name, description, created_at)
                    VALUES (@name, @description, @created_at)
                    RETURNING id;";

                var id = await connection.ExecuteScalarAsync<int>(query, new
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