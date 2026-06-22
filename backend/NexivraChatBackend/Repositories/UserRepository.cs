using Dapper;
using System.Data;
using NexivraChatBackend.Data;
using NexivraChatBackend.Models;

namespace NexivraChatBackend.Repositories
{
    public class UserRepository
    {
        private readonly DapperContext _context;

        public UserRepository(DapperContext context)
        {
            _context = context;
        }

        public User? GetByUsername(string username)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT * FROM users WHERE username = @username LIMIT 1";
                return connection.QueryFirstOrDefault<User>(query, new { username });
            }
        }

        public void Create(User user)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    INSERT INTO users (username, password_hash, created_at) 
                    VALUES (@username, @password_hash, @created_at) 
                    RETURNING id;";
                
                var id = connection.ExecuteScalar<int>(query, new 
                { 
                    username = user.Username, 
                    password_hash = user.PasswordHash, 
                    created_at = DateTime.Now 
                });
                
                user.Id = id;
            }
        }
    }
}
