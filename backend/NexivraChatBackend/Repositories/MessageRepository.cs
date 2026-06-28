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

        public async Task<Message?> GetById(int id)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    SELECT m.id AS Id, m.room_id AS RoomId, m.private_chat_id AS PrivateChatId, m.sender_id AS SenderId,
                           m.sender_name AS SenderName,
                           CASE WHEN m.deleted_at IS NOT NULL THEN '' ELSE m.content END AS Content,
                           m.created_at AS CreatedAt, m.is_ai AS IsAi,
                           m.edited_at AS EditedAt, m.deleted_at AS DeletedAt,
                           m.reply_to_id AS ReplyToId, r.sender_name AS ReplyToSenderName,
                           CASE WHEN r.deleted_at IS NOT NULL THEN NULL ELSE LEFT(r.content, 120) END AS ReplyToContent
                    FROM messages m
                    LEFT JOIN messages r ON m.reply_to_id = r.id
                    WHERE m.id = @id;";
                return await connection.QueryFirstOrDefaultAsync<Message>(query, new { id });
            }
        }

        public async Task<List<Message>> GetOldMessages(int limit, int offset)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    SELECT m.id AS Id, m.room_id AS RoomId, m.private_chat_id AS PrivateChatId, m.sender_id AS SenderId,
                           m.sender_name AS SenderName,
                           CASE WHEN m.deleted_at IS NOT NULL THEN '' ELSE m.content END AS Content,
                           m.created_at AS CreatedAt, m.is_ai AS IsAi,
                           m.edited_at AS EditedAt, m.deleted_at AS DeletedAt,
                           m.reply_to_id AS ReplyToId, r.sender_name AS ReplyToSenderName,
                           CASE WHEN r.deleted_at IS NOT NULL THEN NULL ELSE LEFT(r.content, 120) END AS ReplyToContent
                    FROM messages m
                    LEFT JOIN messages r ON m.reply_to_id = r.id
                    ORDER BY m.created_at DESC LIMIT @limit OFFSET @offset;";
                return (await connection.QueryAsync<Message>(query, new { limit, offset })).ToList();
            }
        }

        public async Task<List<Message>> GetMessagesByRoom(int roomId, int limit = 50, int? beforeId = null, int? afterId = null)
        {
            using (var connection = _context.CreateConnection())
            {
                if (afterId.HasValue)
                {
                    var query = @"
                        SELECT m.id AS Id, m.room_id AS RoomId, m.private_chat_id AS PrivateChatId, m.sender_id AS SenderId,
                               m.sender_name AS SenderName,
                               CASE WHEN m.deleted_at IS NOT NULL THEN '' ELSE m.content END AS Content,
                               m.created_at AS CreatedAt, m.is_ai AS IsAi,
                               m.edited_at AS EditedAt, m.deleted_at AS DeletedAt,
                               m.reply_to_id AS ReplyToId, r.sender_name AS ReplyToSenderName,
                               CASE WHEN r.deleted_at IS NOT NULL THEN NULL ELSE LEFT(r.content, 120) END AS ReplyToContent
                        FROM messages m
                        LEFT JOIN messages r ON m.reply_to_id = r.id
                        WHERE m.room_id = @roomId AND m.id > @afterId ORDER BY m.id ASC LIMIT @limit;";
                    return (await connection.QueryAsync<Message>(query, new { roomId, limit, afterId })).ToList();
                }
                else
                {
                    var query = @"
                        SELECT m.id AS Id, m.room_id AS RoomId, m.private_chat_id AS PrivateChatId, m.sender_id AS SenderId,
                               m.sender_name AS SenderName,
                               CASE WHEN m.deleted_at IS NOT NULL THEN '' ELSE m.content END AS Content,
                               m.created_at AS CreatedAt, m.is_ai AS IsAi,
                               m.edited_at AS EditedAt, m.deleted_at AS DeletedAt,
                               m.reply_to_id AS ReplyToId, r.sender_name AS ReplyToSenderName,
                               CASE WHEN r.deleted_at IS NOT NULL THEN NULL ELSE LEFT(r.content, 120) END AS ReplyToContent
                        FROM messages m
                        LEFT JOIN messages r ON m.reply_to_id = r.id
                        WHERE m.room_id = @roomId AND (@beforeId IS NULL OR m.id < @beforeId) ORDER BY m.id DESC LIMIT @limit;";
                    var result = (await connection.QueryAsync<Message>(query, new { roomId, limit, beforeId })).ToList();
                    result.Reverse(); // Trả về theo thứ tự thời gian tăng dần
                    return result;
                }
            }
        }

        public async Task<List<Message>> GetMessagesByPrivateChat(int privateChatId, int limit = 50, int? beforeId = null, int? afterId = null)
        {
            using (var connection = _context.CreateConnection())
            {
                if (afterId.HasValue)
                {
                    var query = @"
                        SELECT m.id AS Id, m.room_id AS RoomId, m.private_chat_id AS PrivateChatId, m.sender_id AS SenderId,
                               m.sender_name AS SenderName,
                               CASE WHEN m.deleted_at IS NOT NULL THEN '' ELSE m.content END AS Content,
                               m.created_at AS CreatedAt, m.is_ai AS IsAi,
                               m.edited_at AS EditedAt, m.deleted_at AS DeletedAt,
                               m.reply_to_id AS ReplyToId, r.sender_name AS ReplyToSenderName,
                               CASE WHEN r.deleted_at IS NOT NULL THEN NULL ELSE LEFT(r.content, 120) END AS ReplyToContent
                        FROM messages m
                        LEFT JOIN messages r ON m.reply_to_id = r.id
                        WHERE m.private_chat_id = @privateChatId AND m.id > @afterId ORDER BY m.id ASC LIMIT @limit;";
                    return (await connection.QueryAsync<Message>(query, new { privateChatId, limit, afterId })).ToList();
                }
                else
                {
                    var query = @"
                        SELECT m.id AS Id, m.room_id AS RoomId, m.private_chat_id AS PrivateChatId, m.sender_id AS SenderId,
                               m.sender_name AS SenderName,
                               CASE WHEN m.deleted_at IS NOT NULL THEN '' ELSE m.content END AS Content,
                               m.created_at AS CreatedAt, m.is_ai AS IsAi,
                               m.edited_at AS EditedAt, m.deleted_at AS DeletedAt,
                               m.reply_to_id AS ReplyToId, r.sender_name AS ReplyToSenderName,
                               CASE WHEN r.deleted_at IS NOT NULL THEN NULL ELSE LEFT(r.content, 120) END AS ReplyToContent
                        FROM messages m
                        LEFT JOIN messages r ON m.reply_to_id = r.id
                        WHERE m.private_chat_id = @privateChatId AND (@beforeId IS NULL OR m.id < @beforeId) ORDER BY m.id DESC LIMIT @limit;";
                    var result = (await connection.QueryAsync<Message>(query, new { privateChatId, limit, beforeId })).ToList();
                    result.Reverse(); // Trả về theo thứ tự thời gian tăng dần
                    return result;
                }
            }
        }

        public async Task SaveNewMessage(Message message)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    INSERT INTO messages (room_id, private_chat_id, sender_id, sender_name, content, created_at, is_ai, reply_to_id)
                    VALUES (@room_id, @private_chat_id, @sender_id, @sender_name, @content, @created_at, @is_ai, @reply_to_id)
                    RETURNING id;";

                var id = await connection.ExecuteScalarAsync<int>(query, new
                {
                    room_id = message.RoomId,
                    private_chat_id = message.PrivateChatId,
                    sender_id = message.SenderId,
                    sender_name = message.SenderName,
                    content = message.Content,
                    created_at = message.CreatedAt == default ? DateTime.Now : message.CreatedAt,
                    is_ai = message.IsAi,
                    reply_to_id = message.ReplyToId
                });
                message.Id = id;
            }
        }

        public async Task<int> EditMessage(int messageId, int userId, string newContent)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    UPDATE messages
                    SET content = @newContent, edited_at = NOW()
                    WHERE id = @messageId AND sender_id = @userId AND deleted_at IS NULL;";
                return await connection.ExecuteAsync(query, new { messageId, userId, newContent });
            }
        }

        public async Task<int> SoftDeleteMessage(int messageId, int userId)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    UPDATE messages
                    SET deleted_at = NOW()
                    WHERE id = @messageId AND sender_id = @userId AND deleted_at IS NULL;";
                return await connection.ExecuteAsync(query, new { messageId, userId });
            }
        }

        public async Task<List<Message>> GetLatestMessagesBySender(string senderName, int limit = 30)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    SELECT m.id AS Id, m.room_id AS RoomId, m.private_chat_id AS PrivateChatId, m.sender_id AS SenderId,
                           m.sender_name AS SenderName,
                           CASE WHEN m.deleted_at IS NOT NULL THEN '' ELSE m.content END AS Content,
                           m.created_at AS CreatedAt, m.is_ai AS IsAi,
                           m.edited_at AS EditedAt, m.deleted_at AS DeletedAt,
                           m.reply_to_id AS ReplyToId, r.sender_name AS ReplyToSenderName,
                           CASE WHEN r.deleted_at IS NOT NULL THEN NULL ELSE LEFT(r.content, 120) END AS ReplyToContent
                    FROM messages m
                    LEFT JOIN messages r ON m.reply_to_id = r.id
                    WHERE m.sender_name = @senderName AND m.is_ai = false ORDER BY m.created_at DESC LIMIT @limit;";
                return (await connection.QueryAsync<Message>(query, new { senderName, limit })).ToList();
            }
        }
    }
}
