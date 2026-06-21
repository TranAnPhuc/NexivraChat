using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
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
                return connection.Query<Message>(query, new { limit = limit, offset = offset }).ToList();
            }
        }

        public void SaveNewMessage(Message message)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "INSERT INTO messages (content, created_at) VALUES (@content, @created_at)";
                connection.Execute(query, new { content = message.Content, created_at = DateTime.Now });
            }
        }
    }
}
