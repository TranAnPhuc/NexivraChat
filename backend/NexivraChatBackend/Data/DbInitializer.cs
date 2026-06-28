using Dapper;
using System;
using System.Data;

namespace NexivraChatBackend.Data
{
    public static class DbInitializer
    {
        public static void Initialize(DapperContext context)
        {
            using (var connection = context.CreateConnection())
            {
                // 1. Tạo bảng users
                var createUsersTable = @"
                    CREATE TABLE IF NOT EXISTS users (
                        id SERIAL PRIMARY KEY,
                        username VARCHAR(50) UNIQUE NOT NULL,
                        password_hash VARCHAR(255) NOT NULL,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL
                    );";

                // 2. Tạo bảng chat_rooms
                var createRoomsTable = @"
                    CREATE TABLE IF NOT EXISTS chat_rooms (
                        id SERIAL PRIMARY KEY,
                        name VARCHAR(100) NOT NULL,
                        description VARCHAR(255),
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL
                    );";

                // 3. Tạo bảng private_chats
                var createPrivateChatsTable = @"
                    CREATE TABLE IF NOT EXISTS private_chats (
                        id SERIAL PRIMARY KEY,
                        user1_id INT REFERENCES users(id) ON DELETE CASCADE,
                        user2_id INT REFERENCES users(id) ON DELETE CASCADE,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                        CONSTRAINT unique_users UNIQUE (user1_id, user2_id)
                    );";

                // 4. Tạo bảng messages
                var createMessagesTable = @"
                    CREATE TABLE IF NOT EXISTS messages (
                        id SERIAL PRIMARY KEY,
                        room_id INT REFERENCES chat_rooms(id) ON DELETE CASCADE,
                        private_chat_id INT REFERENCES private_chats(id) ON DELETE CASCADE,
                        sender_id INT NULL REFERENCES users(id) ON DELETE SET NULL,
                        sender_name VARCHAR(50) NOT NULL,
                        content TEXT NOT NULL,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                        is_ai BOOLEAN DEFAULT FALSE NOT NULL
                    );";

                // 5. Tạo bảng user_profiles
                var createUserProfilesTable = @"
                    CREATE TABLE IF NOT EXISTS user_profiles (
                        user_id INT PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
                        bio VARCHAR(255),
                        native_language VARCHAR(50) DEFAULT 'Vietnamese' NOT NULL,
                        ai_analysis_json JSONB,
                        last_analyzed_at TIMESTAMP
                    );";

                // 6. Tạo bảng conversation_reads (theo dõi "đã đọc đến đâu" cho mỗi user × hội thoại).
                //    Đúng 1 trong (room_id, private_chat_id) khác NULL (CHECK). Unread tính bằng
                //    số messages có id > last_read_message_id.
                // 6. Tạo bảng conversation_reads (theo dõi "đã đọc đến đâu" cho mỗi user × hội thoại).
                //    Đúng 1 trong (room_id, private_chat_id) khác NULL (CHECK). Unread tính bằng
                //    số messages có id > last_read_message_id.
                var createConversationReadsTable = @"
                    CREATE TABLE IF NOT EXISTS conversation_reads (
                        user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                        room_id INT REFERENCES chat_rooms(id) ON DELETE CASCADE,
                        private_chat_id INT REFERENCES private_chats(id) ON DELETE CASCADE,
                        last_read_message_id INT NOT NULL DEFAULT 0,
                        updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                        CONSTRAINT chk_reads_one_target CHECK ((room_id IS NULL) <> (private_chat_id IS NULL))
                    );";

                // 7. Tạo bảng message_reactions (lưu cảm xúc emoji thả trên tin nhắn)
                var createMessageReactionsTable = @"
                    CREATE TABLE IF NOT EXISTS message_reactions (
                        message_id INT NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
                        user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                        emoji VARCHAR(16) NOT NULL,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                        PRIMARY KEY (message_id, user_id, emoji)
                    );";

                // 8. Tạo bảng message_mentions (lưu nhắc tên user trong tin nhắn phòng)
                var createMessageMentionsTable = @"
                    CREATE TABLE IF NOT EXISTS message_mentions (
                        message_id INT NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
                        mentioned_user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                        PRIMARY KEY (message_id, mentioned_user_id)
                    );";

                connection.Execute(createUsersTable);
                connection.Execute(createRoomsTable);
                connection.Execute(createPrivateChatsTable);
                connection.Execute(createMessagesTable);
                connection.Execute(createUserProfilesTable);
                connection.Execute(createConversationReadsTable);
                connection.Execute(createMessageReactionsTable);
                connection.Execute(createMessageMentionsTable);

                // Migration idempotent bổ sung sender_id, reply_to_id, edited_at, deleted_at cho bảng messages
                var migrateMessageColumns = @"
                    ALTER TABLE messages ADD COLUMN IF NOT EXISTS sender_id INT NULL REFERENCES users(id) ON DELETE SET NULL;
                    ALTER TABLE messages ADD COLUMN IF NOT EXISTS reply_to_id INT NULL REFERENCES messages(id) ON DELETE SET NULL;
                    ALTER TABLE messages ADD COLUMN IF NOT EXISTS edited_at TIMESTAMP NULL;
                    ALTER TABLE messages ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMP NULL;
                    UPDATE messages m
                    SET sender_id = u.id
                    FROM users u
                    WHERE m.sender_id IS NULL AND m.is_ai = false AND m.sender_name = u.username;";
                connection.Execute(migrateMessageColumns);

                // 7. Tạo index tối ưu truy vấn lịch sử tin nhắn (idempotent)
                var createMessageIndexes = @"
                    CREATE INDEX IF NOT EXISTS idx_messages_room_created
                        ON messages (room_id, created_at);
                    CREATE INDEX IF NOT EXISTS idx_messages_private_created
                        ON messages (private_chat_id, created_at);
                    CREATE INDEX IF NOT EXISTS idx_messages_sender
                        ON messages (sender_name);
                    CREATE INDEX IF NOT EXISTS idx_messages_sender_id
                        ON messages (sender_id);
                    CREATE INDEX IF NOT EXISTS idx_messages_reply_to
                        ON messages (reply_to_id);
                    CREATE INDEX IF NOT EXISTS idx_reactions_message
                        ON message_reactions (message_id);
                    -- Phục vụ truy vấn unread (id > last_read), không phải theo created_at
                    CREATE INDEX IF NOT EXISTS idx_messages_room_id
                        ON messages (room_id, id);
                    CREATE INDEX IF NOT EXISTS idx_messages_private_id
                        ON messages (private_chat_id, id);
                    CREATE INDEX IF NOT EXISTS idx_mentions_user
                        ON message_mentions (mentioned_user_id);";
                connection.Execute(createMessageIndexes);

                // 8. Index unique từng phần cho conversation_reads (NULL bị coi distinct nên
                //    UNIQUE(user_id, room_id) thường sẽ cho trùng hàng DM — phải dùng partial index).
                var createConversationReadIndexes = @"
                    CREATE UNIQUE INDEX IF NOT EXISTS uq_reads_room
                        ON conversation_reads (user_id, room_id) WHERE room_id IS NOT NULL;
                    CREATE UNIQUE INDEX IF NOT EXISTS uq_reads_dm
                        ON conversation_reads (user_id, private_chat_id) WHERE private_chat_id IS NOT NULL;";
                connection.Execute(createConversationReadIndexes);

                // 4. Seed phòng mặc định nếu rỗng
                var checkRooms = "SELECT COUNT(*) FROM chat_rooms";
                var roomCount = connection.ExecuteScalar<int>(checkRooms);
                if (roomCount == 0)
                {
                    var seedRooms = @"
                        INSERT INTO chat_rooms (name, description) VALUES 
                        ('General', 'Kênh thảo luận chung dành cho tất cả mọi người.'),
                        ('AI Lounge', 'Kênh chat và thử nghiệm trợ lý ảo AI Copilot.');";
                    connection.Execute(seedRooms);
                }
            }
        }
    }
}
