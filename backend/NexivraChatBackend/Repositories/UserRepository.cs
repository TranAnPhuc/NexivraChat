using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public async Task<User?> GetByUsername(string username)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT id, username, password_hash, created_at FROM users WHERE username = @username LIMIT 1";
                return await connection.QueryFirstOrDefaultAsync<User>(query, new { username });
            }
        }

        public async Task<User?> GetById(int id)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT id, username, created_at AS CreatedAt FROM users WHERE id = @id LIMIT 1";
                return await connection.QueryFirstOrDefaultAsync<User>(query, new { id });
            }
        }

        public async Task<List<User>> GetAll()
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT id, username, created_at AS CreatedAt FROM users ORDER BY username ASC";
                return (await connection.QueryAsync<User>(query)).ToList();
            }
        }

        public async Task Create(User user)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    INSERT INTO users (username, password_hash, created_at)
                    VALUES (@username, @password_hash, @created_at)
                    RETURNING id;";

                var id = await connection.ExecuteScalarAsync<int>(query, new
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
