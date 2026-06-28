using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NexivraChatBackend.Data;
using NexivraChatBackend.Models;

namespace NexivraChatBackend.Repositories
{
    // Theo dõi "đã đọc đến đâu" cho mỗi user × hội thoại và tính số tin chưa đọc.
    // Unread = số messages có id > last_read_message_id và không phải do chính mình gửi.
    public class ConversationReadRepository
    {
        private readonly DapperContext _context;

        public ConversationReadRepository(DapperContext context)
        {
            _context = context;
        }

        // Hàng phụ trợ map kết quả GROUP BY.
        private class UnreadRow
        {
            public int ConversationId { get; set; }
            public int Count { get; set; }
        }

        // Lấy unread cho toàn bộ hội thoại của 1 user.
        // - Phòng: INNER JOIN conversation_reads → phòng CHƯA mở (không có read row) = 0/ẩn
        //   (quyết định cold-start: không "đứng sau" một phòng công khai chưa từng vào).
        // - DM: LEFT JOIN + COALESCE(last_read, 0), scope theo participant → DM mới từ người
        //   khác (chưa mở) vẫn tính đủ unread, vì "có người nhắn cho bạn" là thứ không được bỏ lỡ.
        public async Task<UnreadCounts> GetUnreadCounts(int userId, string username)
        {
            using (var connection = _context.CreateConnection())
            {
                var roomQuery = @"
                    SELECT m.room_id AS ConversationId, COUNT(*) AS Count
                    FROM messages m
                    JOIN conversation_reads cr
                      ON cr.user_id = @userId AND cr.room_id = m.room_id
                    WHERE m.room_id IS NOT NULL
                      AND m.id > cr.last_read_message_id
                      AND m.sender_name <> @username
                    GROUP BY m.room_id;";

                // Key theo ID NGƯỜI ĐỐI THOẠI (không phải private_chat_id) để sidebar
                // (liệt kê theo user) tra cứu badge trực tiếp.
                var dmQuery = @"
                    SELECT (CASE WHEN pc.user1_id = @userId THEN pc.user2_id ELSE pc.user1_id END) AS ConversationId,
                           COUNT(*) AS Count
                    FROM messages m
                    JOIN private_chats pc
                      ON pc.id = m.private_chat_id
                     AND (pc.user1_id = @userId OR pc.user2_id = @userId)
                    LEFT JOIN conversation_reads cr
                      ON cr.user_id = @userId AND cr.private_chat_id = m.private_chat_id
                    WHERE m.id > COALESCE(cr.last_read_message_id, 0)
                      AND m.sender_name <> @username
                    GROUP BY (CASE WHEN pc.user1_id = @userId THEN pc.user2_id ELSE pc.user1_id END);";

                var rooms = (await connection.QueryAsync<UnreadRow>(roomQuery, new { userId, username })).ToList();
                var dms = (await connection.QueryAsync<UnreadRow>(dmQuery, new { userId, username })).ToList();

                return new UnreadCounts
                {
                    Rooms = rooms.ToDictionary(r => r.ConversationId, r => r.Count),
                    PrivateChats = dms.ToDictionary(r => r.ConversationId, r => r.Count)
                };
            }
        }

        // Đánh dấu đã đọc tới lastReadMessageId. GREATEST đảm bảo không lùi mốc đọc
        // (một MarkRead đến trễ với id cũ hơn không "un-read"). ON CONFLICT nhắm đúng
        // partial unique index theo từng loại hội thoại.
        public async Task MarkRoomRead(int userId, int roomId, int lastReadMessageId)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    INSERT INTO conversation_reads (user_id, room_id, last_read_message_id, updated_at)
                    VALUES (@userId, @roomId, @lastReadMessageId, @now)
                    ON CONFLICT (user_id, room_id) WHERE room_id IS NOT NULL
                    DO UPDATE SET
                        last_read_message_id = GREATEST(conversation_reads.last_read_message_id, EXCLUDED.last_read_message_id),
                        updated_at = EXCLUDED.updated_at;";
                await connection.ExecuteAsync(query, new { userId, roomId, lastReadMessageId, now = DateTime.Now });
            }
        }

        public async Task MarkPrivateChatRead(int userId, int privateChatId, int lastReadMessageId)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    INSERT INTO conversation_reads (user_id, private_chat_id, last_read_message_id, updated_at)
                    VALUES (@userId, @privateChatId, @lastReadMessageId, @now)
                    ON CONFLICT (user_id, private_chat_id) WHERE private_chat_id IS NOT NULL
                    DO UPDATE SET
                        last_read_message_id = GREATEST(conversation_reads.last_read_message_id, EXCLUDED.last_read_message_id),
                        updated_at = EXCLUDED.updated_at;";
                await connection.ExecuteAsync(query, new { userId, privateChatId, lastReadMessageId, now = DateTime.Now });
            }
        }

        public async Task<int> GetPartnerLastReadMessageId(int userId, int partnerUserId)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    SELECT COALESCE(cr.last_read_message_id, 0)
                    FROM private_chats pc
                    LEFT JOIN conversation_reads cr
                      ON cr.user_id = @partnerUserId AND cr.private_chat_id = pc.id
                    WHERE (pc.user1_id = @userId AND pc.user2_id = @partnerUserId)
                       OR (pc.user1_id = @partnerUserId AND pc.user2_id = @userId);";
                return await connection.ExecuteScalarAsync<int>(query, new { userId, partnerUserId });
            }
        }
    }
}
