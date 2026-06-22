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

                // 3. Tạo bảng messages
                var createMessagesTable = @"
                    CREATE TABLE IF NOT EXISTS messages (
                        id SERIAL PRIMARY KEY,
                        room_id INT REFERENCES chat_rooms(id) ON DELETE CASCADE,
                        sender_name VARCHAR(50) NOT NULL,
                        content TEXT NOT NULL,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                        is_ai BOOLEAN DEFAULT FALSE NOT NULL
                    );";

                connection.Execute(createUsersTable);
                connection.Execute(createRoomsTable);
                connection.Execute(createMessagesTable);
                
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
