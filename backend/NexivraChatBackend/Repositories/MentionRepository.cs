using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NexivraChatBackend.Data;

namespace NexivraChatBackend.Repositories
{
    public class MentionRepository
    {
        private readonly DapperContext _context;

        public MentionRepository(DapperContext context)
        {
            _context = context;
        }

        public async Task SaveMentions(int messageId, IEnumerable<int> userIds)
        {
            var distinctUserIds = userIds.Distinct().ToList();
            if (!distinctUserIds.Any()) return;

            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    INSERT INTO message_mentions (message_id, mentioned_user_id)
                    VALUES (@messageId, @userId)
                    ON CONFLICT DO NOTHING;";

                foreach (var userId in distinctUserIds)
                {
                    await connection.ExecuteAsync(query, new { messageId, userId });
                }
            }
        }

        public async Task<List<int>> GetUnreadMentionRoomIds(int userId)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    SELECT DISTINCT m.room_id
                    FROM message_mentions mm
                    JOIN messages m ON mm.message_id = m.id
                    LEFT JOIN conversation_reads cr ON cr.user_id = @userId AND cr.room_id = m.room_id
                    WHERE mm.mentioned_user_id = @userId
                      AND m.room_id IS NOT NULL
                      AND m.id > COALESCE(cr.last_read_message_id, 0);";

                return (await connection.QueryAsync<int>(query, new { userId })).ToList();
            }
        }
    }
}
