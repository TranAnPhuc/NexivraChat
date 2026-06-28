using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NexivraChatBackend.Data;
using NexivraChatBackend.Models;

namespace NexivraChatBackend.Repositories
{
    public class ConversationLookup
    {
        public int? RoomId { get; set; }
        public int? PrivateChatId { get; set; }
    }

    public class ReactionRepository
    {
        private readonly DapperContext _context;

        public ReactionRepository(DapperContext context)
        {
            _context = context;
        }

        public async Task<(bool Reacted, int NewCount)> ToggleReaction(int messageId, int userId, string emoji)
        {
            using (var connection = _context.CreateConnection())
            {
                var checkQuery = "SELECT COUNT(1) FROM message_reactions WHERE message_id = @messageId AND user_id = @userId AND emoji = @emoji;";
                var exists = await connection.ExecuteScalarAsync<int>(checkQuery, new { messageId, userId, emoji }) > 0;

                bool reacted;
                if (exists)
                {
                    var deleteQuery = "DELETE FROM message_reactions WHERE message_id = @messageId AND user_id = @userId AND emoji = @emoji;";
                    await connection.ExecuteAsync(deleteQuery, new { messageId, userId, emoji });
                    reacted = false;
                }
                else
                {
                    var insertQuery = "INSERT INTO message_reactions (message_id, user_id, emoji) VALUES (@messageId, @userId, @emoji) ON CONFLICT DO NOTHING;";
                    await connection.ExecuteAsync(insertQuery, new { messageId, userId, emoji });
                    reacted = true;
                }

                var countQuery = "SELECT COUNT(*) FROM message_reactions WHERE message_id = @messageId AND emoji = @emoji;";
                var newCount = await connection.ExecuteScalarAsync<int>(countQuery, new { messageId, emoji });

                return (reacted, newCount);
            }
        }

        public async Task<List<ReactionSummary>> GetReactionsForMessages(IEnumerable<int> messageIds, int currentUserId)
        {
            var ids = messageIds?.ToList();
            if (ids == null || !ids.Any())
            {
                return new List<ReactionSummary>();
            }

            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    SELECT message_id AS MessageId, emoji AS Emoji, COUNT(*) AS Count,
                           BOOL_OR(user_id = @currentUserId) AS MineReacted
                    FROM message_reactions
                    WHERE message_id = ANY(@ids)
                    GROUP BY message_id, emoji;";

                return (await connection.QueryAsync<ReactionSummary>(query, new { ids, currentUserId })).ToList();
            }
        }

        public async Task<ConversationLookup?> LookupConversation(int messageId)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT room_id AS RoomId, private_chat_id AS PrivateChatId FROM messages WHERE id = @messageId;";
                return await connection.QueryFirstOrDefaultAsync<ConversationLookup>(query, new { messageId });
            }
        }
    }
}
