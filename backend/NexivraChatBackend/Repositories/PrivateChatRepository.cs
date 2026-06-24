using Dapper;
using System;
using System.Data;
using System.Linq;
using NexivraChatBackend.Data;
using NexivraChatBackend.Models;

namespace NexivraChatBackend.Repositories
{
    public class PrivateChatRepository
    {
        private readonly DapperContext _context;

        public PrivateChatRepository(DapperContext context)
        {
            _context = context;
        }

        public PrivateChat GetOrCreate(int user1Id, int user2Id)
        {
            // Đảm bảo u1 < u2 để khớp với constraint UNIQUE (user1_id, user2_id)
            int u1 = Math.Min(user1Id, user2Id);
            int u2 = Math.Max(user1Id, user2Id);

            using (var connection = _context.CreateConnection())
            {
                var selectQuery = "SELECT id, user1_id AS User1Id, user2_id AS User2Id, created_at AS CreatedAt FROM private_chats WHERE user1_id = @u1 AND user2_id = @u2 LIMIT 1;";
                var chat = connection.QueryFirstOrDefault<PrivateChat>(selectQuery, new { u1, u2 });
                if (chat != null)
                {
                    return chat;
                }

                var insertQuery = @"
                    INSERT INTO private_chats (user1_id, user2_id, created_at)
                    VALUES (@u1, @u2, @created_at)
                    RETURNING id, user1_id AS User1Id, user2_id AS User2Id, created_at AS CreatedAt;";

                return connection.QuerySingle<PrivateChat>(insertQuery, new 
                { 
                    u1, 
                    u2, 
                    created_at = DateTime.Now 
                });
            }
        }

        public PrivateChat? GetById(int id)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT id, user1_id AS User1Id, user2_id AS User2Id, created_at AS CreatedAt FROM private_chats WHERE id = @id LIMIT 1;";
                return connection.QueryFirstOrDefault<PrivateChat>(query, new { id });
            }
        }
    }
}
