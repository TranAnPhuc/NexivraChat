# Giai đoạn 2 — Cấu trúc Backend (Async I/O) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Async hóa toàn bộ truy cập DB (Dapper) end-to-end và chuyển các HTTP service sang `IHttpClientFactory`, để bỏ block thread pool và hết nguy cơ socket exhaustion.

**Architecture:** Mỗi repository đổi các method sang `Task<...>` dùng Dapper async (`QueryAsync`/`ExecuteScalarAsync`/`QueryFirstOrDefaultAsync`/`QuerySingleAsync`); mọi caller (ChatHub đã async, các Controller chuyển sang `async Task<IActionResult>`) thêm `await`. `AiService` và `TranslationService` nhận `HttpClient` qua DI thay vì `new HttpClient()`. Mỗi repository + caller của nó là một task giữ build xanh độc lập; HttpClientFactory là một task riêng tách biệt.

**Tech Stack:** .NET 8, Dapper (async extensions), Npgsql, ASP.NET Core MVC + SignalR, xUnit.

## Global Constraints

- PostgreSQL + Dapper SQL thuần — tuyệt đối KHÔNG dùng EF Core.
- Cột SQL viết snake_case (Dapper `MatchNamesWithUnderscores = true`). Không đổi nội dung SQL ở GĐ này — chỉ đổi cách gọi (sync → async).
- Async all-the-way: KHÔNG dùng `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`, `Task.Run` để gọi DB. Mọi caller phải `await`.
- Không đổi hành vi nghiệp vụ, không đổi response shape, không thêm tính năng.
- Không có DB integration test trong dự án → verification mỗi task = `dotnet build` (0 errors) + `dotnet test` (8 test có sẵn vẫn xanh). Smoke test thủ công cần app+DB+key; nếu không chạy được thì SKIP và ghi chú, KHÔNG block.
- Lệnh build/test chạy từ `D:\Vibe_Coding\NexivraChat\backend\NexivraChatBackend` (build) và `...\backend\NexivraChatBackend.Tests` (test). KHÔNG tạo file log build trong repo.
- `DbInitializer.cs` (code khởi động một lần) KHÔNG thuộc phạm vi — giữ nguyên đồng bộ.

---

### Task 1: IHttpClientFactory cho AiService + TranslationService

Tách biệt, không đụng DB. Đổi DI registration và constructor của 2 service; method signature của service KHÔNG đổi nên caller (ChatHub, controllers) không cần sửa.

**Files:**
- Modify: `backend/NexivraChatBackend/Program.cs` (dòng 26-27)
- Modify: `backend/NexivraChatBackend/Services/AiService.cs` (constructor, dòng 14-23)
- Modify: `backend/NexivraChatBackend/Services/TranslationService.cs` (constructor, dòng 12-21)

**Interfaces:**
- Consumes: registration `AddHttpClient<T>()` (ASP.NET Core) inject `HttpClient` vào constructor typed client.
- Produces: `AiService(HttpClient httpClient, IConfiguration config)` và `TranslationService(HttpClient httpClient, IConfiguration config)` — chữ ký method công khai (`StreamResponseAsync`, `GenerateContentAsync`, `TranslateTextAsync`) GIỮ NGUYÊN.

- [ ] **Step 1: Đổi DI registration trong `Program.cs`**

Thay 2 dòng (hiện 26-27):
```csharp
builder.Services.AddScoped<AiService>();
builder.Services.AddScoped<TranslationService>();
```
bằng:
```csharp
builder.Services.AddHttpClient<AiService>();
builder.Services.AddHttpClient<TranslationService>();
```
(Giữ nguyên `builder.Services.AddScoped<TokenService>();` ở dòng 25.)

- [ ] **Step 2: Sửa constructor `AiService`**

Trong `backend/NexivraChatBackend/Services/AiService.cs`, thay constructor (dòng 17-23):
```csharp
        public AiService(IConfiguration config)
        {
            _httpClient = new HttpClient();
            var configKey = config["Gemini:ApiKey"];
            var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            _apiKey = !string.IsNullOrEmpty(configKey) ? configKey : (!string.IsNullOrEmpty(envKey) ? envKey : string.Empty);
        }
```
bằng:
```csharp
        public AiService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            var configKey = config["Gemini:ApiKey"];
            var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            _apiKey = !string.IsNullOrEmpty(configKey) ? configKey : (!string.IsNullOrEmpty(envKey) ? envKey : string.Empty);
        }
```
(Field `private readonly HttpClient _httpClient;` ở dòng 14 giữ nguyên.)

- [ ] **Step 3: Sửa constructor `TranslationService`**

Trong `backend/NexivraChatBackend/Services/TranslationService.cs`, thay constructor (dòng 15-21):
```csharp
        public TranslationService(IConfiguration config)
        {
            _httpClient = new HttpClient();
            var configKey = config["Gemini:ApiKey"];
            var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            _apiKey = !string.IsNullOrEmpty(configKey) ? configKey : (!string.IsNullOrEmpty(envKey) ? envKey : string.Empty);
        }
```
bằng:
```csharp
        public TranslationService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            var configKey = config["Gemini:ApiKey"];
            var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            _apiKey = !string.IsNullOrEmpty(configKey) ? configKey : (!string.IsNullOrEmpty(envKey) ? envKey : string.Empty);
        }
```

- [ ] **Step 4: Build + test**

Run: `cd backend/NexivraChatBackend && dotnet build` → Expected: 0 errors.
Run: `cd backend/NexivraChatBackend.Tests && dotnet test` → Expected: Passed 8/8.

- [ ] **Step 5: Xác minh không còn `new HttpClient()`**

Run: `git grep -n "new HttpClient()" backend/NexivraChatBackend/Services/`
Expected: không có kết quả.

- [ ] **Step 6: Commit**

```bash
git add backend/NexivraChatBackend/Program.cs backend/NexivraChatBackend/Services/AiService.cs backend/NexivraChatBackend/Services/TranslationService.cs
git commit -m "perf(http): dùng IHttpClientFactory cho AiService và TranslationService"
```

---

### Task 2: Async MessageRepository + tất cả caller

**Files:**
- Modify: `backend/NexivraChatBackend/Repositories/MessageRepository.cs` (toàn bộ method)
- Modify: `backend/NexivraChatBackend/Hubs/ChatHub.cs` (dòng 105, 120, 166, 201)
- Modify: `backend/NexivraChatBackend/Controllers/RoomsController.cs` (`GetRoomMessages`, dòng 47-58)
- Modify: `backend/NexivraChatBackend/Controllers/UsersController.cs` (`GetPrivateChatMessages`, dòng 68-91)
- Modify: `backend/NexivraChatBackend/Controllers/ProfileController.cs` (`AnalyzeProfile`, dòng 161)

**Interfaces:**
- Produces (MessageRepository, tất cả method giữ tên & tham số, chỉ đổi kiểu trả về):
  - `Task<List<Message>> GetOldMessages(int limit, int offset)`
  - `Task<List<Message>> GetMessagesByRoom(int roomId, int limit = 50, int offset = 0)`
  - `Task<List<Message>> GetMessagesByPrivateChat(int privateChatId, int limit = 50, int offset = 0)`
  - `Task SaveNewMessage(Message message)` (vẫn gán `message.Id`)
  - `Task<List<Message>> GetLatestMessagesBySender(string senderName, int limit = 30)`

- [ ] **Step 1: Thay toàn bộ thân class `MessageRepository`**

Ghi đè `backend/NexivraChatBackend/Repositories/MessageRepository.cs` bằng:
```csharp
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

        public async Task<List<Message>> GetOldMessages(int limit, int offset)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT id, room_id, private_chat_id, sender_name, content, created_at, is_ai FROM messages ORDER BY created_at DESC LIMIT @limit OFFSET @offset";
                return (await connection.QueryAsync<Message>(query, new { limit, offset })).ToList();
            }
        }

        public async Task<List<Message>> GetMessagesByRoom(int roomId, int limit = 50, int offset = 0)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT id, room_id, private_chat_id, sender_name, content, created_at, is_ai FROM messages WHERE room_id = @roomId ORDER BY created_at DESC LIMIT @limit OFFSET @offset";
                var result = (await connection.QueryAsync<Message>(query, new { roomId, limit, offset })).ToList();
                result.Reverse(); // Trả về theo thứ tự thời gian tăng dần
                return result;
            }
        }

        public async Task<List<Message>> GetMessagesByPrivateChat(int privateChatId, int limit = 50, int offset = 0)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT id, room_id, private_chat_id, sender_name, content, created_at, is_ai FROM messages WHERE private_chat_id = @privateChatId ORDER BY created_at DESC LIMIT @limit OFFSET @offset";
                var result = (await connection.QueryAsync<Message>(query, new { privateChatId, limit, offset })).ToList();
                result.Reverse(); // Trả về theo thứ tự thời gian tăng dần
                return result;
            }
        }

        public async Task SaveNewMessage(Message message)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    INSERT INTO messages (room_id, private_chat_id, sender_name, content, created_at, is_ai) 
                    VALUES (@room_id, @private_chat_id, @sender_name, @content, @created_at, @is_ai)
                    RETURNING id;";

                var id = await connection.ExecuteScalarAsync<int>(query, new
                {
                    room_id = message.RoomId,
                    private_chat_id = message.PrivateChatId,
                    sender_name = message.SenderName,
                    content = message.Content,
                    created_at = message.CreatedAt == default ? DateTime.Now : message.CreatedAt,
                    is_ai = message.IsAi
                });
                message.Id = id;
            }
        }

        public async Task<List<Message>> GetLatestMessagesBySender(string senderName, int limit = 30)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT id, room_id, private_chat_id, sender_name, content, created_at, is_ai FROM messages WHERE sender_name = @senderName AND is_ai = false ORDER BY created_at DESC LIMIT @limit";
                return (await connection.QueryAsync<Message>(query, new { senderName, limit })).ToList();
            }
        }
    }
}
```

- [ ] **Step 2: Thêm `await` ở 4 call site trong `ChatHub.cs`**

(`SendMessage` và `SendPrivateMessage` đã là `async Task` nên chỉ thêm `await`.)
- Dòng 105: `_messageRepository.SaveNewMessage(userMessage);` → `await _messageRepository.SaveNewMessage(userMessage);`
- Dòng 120: `var recentMessages = _messageRepository.GetMessagesByRoom(roomId, 10, 0);` → `var recentMessages = await _messageRepository.GetMessagesByRoom(roomId, 10, 0);`
- Dòng 166: `_messageRepository.SaveNewMessage(finalAiMessage);` → `await _messageRepository.SaveNewMessage(finalAiMessage);`
- Dòng 201: `_messageRepository.SaveNewMessage(userMessage);` → `await _messageRepository.SaveNewMessage(userMessage);`

- [ ] **Step 3: Chuyển `RoomsController.GetRoomMessages` sang async**

Thay (dòng 47-58):
```csharp
        [HttpGet("{id}/messages")]
        public IActionResult GetRoomMessages(int id, [FromQuery] int limit = 50, [FromQuery] int offset = 0)
        {
            var room = _roomRepository.GetById(id);
            if (room == null)
            {
                return NotFound("Phòng chat không tồn tại.");
            }

            var messages = _messageRepository.GetMessagesByRoom(id, limit, offset);
            return Ok(messages);
        }
```
bằng:
```csharp
        [HttpGet("{id}/messages")]
        public async Task<IActionResult> GetRoomMessages(int id, [FromQuery] int limit = 50, [FromQuery] int offset = 0)
        {
            var room = _roomRepository.GetById(id);
            if (room == null)
            {
                return NotFound("Phòng chat không tồn tại.");
            }

            var messages = await _messageRepository.GetMessagesByRoom(id, limit, offset);
            return Ok(messages);
        }
```
Thêm `using System.Threading.Tasks;` vào đầu file `RoomsController.cs` nếu chưa có.
(`_roomRepository.GetById` vẫn đồng bộ ở task này — sẽ async ở Task 4.)

- [ ] **Step 4: Chuyển `UsersController.GetPrivateChatMessages` sang async**

Thay dòng signature (dòng 69):
```csharp
        public IActionResult GetPrivateChatMessages(int id, [FromQuery] int limit = 50, [FromQuery] int offset = 0)
```
bằng:
```csharp
        public async Task<IActionResult> GetPrivateChatMessages(int id, [FromQuery] int limit = 50, [FromQuery] int offset = 0)
```
Và thay (dòng 89):
```csharp
            var messages = _messageRepository.GetMessagesByPrivateChat(id, limit, offset);
```
bằng:
```csharp
            var messages = await _messageRepository.GetMessagesByPrivateChat(id, limit, offset);
```
Thêm `using System.Threading.Tasks;` vào đầu `UsersController.cs` nếu chưa có.
(`_privateChatRepository.GetById` ở dòng 71 vẫn đồng bộ — sẽ async ở Task 5.)

- [ ] **Step 5: Thêm `await` ở `ProfileController.AnalyzeProfile`**

(`AnalyzeProfile` đã là `async Task<IActionResult>`.) Dòng 161:
```csharp
            var latestMessages = _messageRepository.GetLatestMessagesBySender(username, 30);
```
→
```csharp
            var latestMessages = await _messageRepository.GetLatestMessagesBySender(username, 30);
```

- [ ] **Step 6: Build + test**

Run: `cd backend/NexivraChatBackend && dotnet build` → 0 errors.
Run: `cd backend/NexivraChatBackend.Tests && dotnet test` → Passed 8/8.
(Nếu thấy warning CS1998 "async method lacks await" ở bất kỳ method nào bạn vừa đổi async → nghĩa là quên `await` ở đó; sửa lại.)

- [ ] **Step 7: Commit**

```bash
git add backend/NexivraChatBackend/Repositories/MessageRepository.cs backend/NexivraChatBackend/Hubs/ChatHub.cs backend/NexivraChatBackend/Controllers/RoomsController.cs backend/NexivraChatBackend/Controllers/UsersController.cs backend/NexivraChatBackend/Controllers/ProfileController.cs
git commit -m "perf(db): async hóa MessageRepository và các caller"
```

---

### Task 3: Async UserRepository + tất cả caller

**Files:**
- Modify: `backend/NexivraChatBackend/Repositories/UserRepository.cs`
- Modify: `backend/NexivraChatBackend/Controllers/AuthController.cs` (`Register` dòng 24-49, `Login` dòng 51-73)
- Modify: `backend/NexivraChatBackend/Controllers/UsersController.cs` (`GetUsers`, dòng 30-48)
- Modify: `backend/NexivraChatBackend/Controllers/ProfileController.cs` (`GetUserProfile`, dòng 71-103)

**Interfaces:**
- Produces (UserRepository):
  - `Task<User?> GetByUsername(string username)`
  - `Task<List<User>> GetAll()`
  - `Task Create(User user)` (vẫn gán `user.Id`)

- [ ] **Step 1: Thay toàn bộ thân class `UserRepository`**

Ghi đè `backend/NexivraChatBackend/Repositories/UserRepository.cs` bằng:
```csharp
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
```

- [ ] **Step 2: Chuyển `AuthController.Register` + `Login` sang async**

Thay signature `Register` (dòng 25):
```csharp
        public IActionResult Register([FromBody] RegisterDto dto)
```
→
```csharp
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
```
Trong `Register`, thay 2 call:
- dòng 32 `var existingUser = _userRepository.GetByUsername(dto.Username);` → `var existingUser = await _userRepository.GetByUsername(dto.Username);`
- dòng 45 `_userRepository.Create(user);` → `await _userRepository.Create(user);`

Thay signature `Login` (dòng 52):
```csharp
        public IActionResult Login([FromBody] LoginDto dto)
```
→
```csharp
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
```
Trong `Login`, thay dòng 59 `var user = _userRepository.GetByUsername(dto.Username);` → `var user = await _userRepository.GetByUsername(dto.Username);`

Thêm `using System.Threading.Tasks;` vào đầu `AuthController.cs` nếu chưa có.

- [ ] **Step 3: Chuyển `UsersController.GetUsers` sang async**

Thay signature (dòng 31):
```csharp
        public IActionResult GetUsers()
```
→
```csharp
        public async Task<IActionResult> GetUsers()
```
Thay dòng 39:
```csharp
            var allUsers = _userRepository.GetAll();
```
→
```csharp
            var allUsers = await _userRepository.GetAll();
```

- [ ] **Step 4: Chuyển `ProfileController.GetUserProfile` sang async**

Thay signature (dòng 72):
```csharp
        public IActionResult GetUserProfile(int userId)
```
→
```csharp
        public async Task<IActionResult> GetUserProfile(int userId)
```
Thay dòng 74:
```csharp
            var allUsers = _userRepository.GetAll();
```
→
```csharp
            var allUsers = await _userRepository.GetAll();
```

- [ ] **Step 5: Build + test**

Run: `cd backend/NexivraChatBackend && dotnet build` → 0 errors.
Run: `cd backend/NexivraChatBackend.Tests && dotnet test` → Passed 8/8.

- [ ] **Step 6: Commit**

```bash
git add backend/NexivraChatBackend/Repositories/UserRepository.cs backend/NexivraChatBackend/Controllers/AuthController.cs backend/NexivraChatBackend/Controllers/UsersController.cs backend/NexivraChatBackend/Controllers/ProfileController.cs
git commit -m "perf(db): async hóa UserRepository và các caller"
```

---

### Task 4: Async RoomRepository + tất cả caller

**Files:**
- Modify: `backend/NexivraChatBackend/Repositories/RoomRepository.cs`
- Modify: `backend/NexivraChatBackend/Controllers/RoomsController.cs` (`GetRooms` dòng 22-27, `CreateRoom` dòng 29-45, và call `GetById` trong `GetRoomMessages` dòng 50)

**Interfaces:**
- Produces (RoomRepository):
  - `Task<List<ChatRoom>> GetAll()`
  - `Task<ChatRoom?> GetById(int id)`
  - `Task Create(ChatRoom room)` (vẫn gán `room.Id`)

- [ ] **Step 1: Thay toàn bộ thân class `RoomRepository`**

Ghi đè `backend/NexivraChatBackend/Repositories/RoomRepository.cs` bằng:
```csharp
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NexivraChatBackend.Data;
using NexivraChatBackend.Models;

namespace NexivraChatBackend.Repositories
{
    public class RoomRepository
    {
        private readonly DapperContext _context;

        public RoomRepository(DapperContext context)
        {
            _context = context;
        }

        public async Task<List<ChatRoom>> GetAll()
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT id, name, description FROM chat_rooms ORDER BY name ASC";
                return (await connection.QueryAsync<ChatRoom>(query)).ToList();
            }
        }

        public async Task<ChatRoom?> GetById(int id)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT id, name, description FROM chat_rooms WHERE id = @id LIMIT 1";
                return await connection.QueryFirstOrDefaultAsync<ChatRoom>(query, new { id });
            }
        }

        public async Task Create(ChatRoom room)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    INSERT INTO chat_rooms (name, description, created_at) 
                    VALUES (@name, @description, @created_at) 
                    RETURNING id;";

                var id = await connection.ExecuteScalarAsync<int>(query, new
                {
                    name = room.Name,
                    description = room.Description,
                    created_at = DateTime.Now
                });

                room.Id = id;
            }
        }
    }
}
```

- [ ] **Step 2: Chuyển `RoomsController.GetRooms` + `CreateRoom` sang async; await `GetById`**

`GetRooms` — thay (dòng 22-27):
```csharp
        [HttpGet]
        public IActionResult GetRooms()
        {
            var rooms = _roomRepository.GetAll();
            return Ok(rooms);
        }
```
bằng:
```csharp
        [HttpGet]
        public async Task<IActionResult> GetRooms()
        {
            var rooms = await _roomRepository.GetAll();
            return Ok(rooms);
        }
```

`CreateRoom` — thay signature (dòng 30):
```csharp
        public IActionResult CreateRoom([FromBody] CreateRoomDto dto)
```
→
```csharp
        public async Task<IActionResult> CreateRoom([FromBody] CreateRoomDto dto)
```
và thay dòng 43:
```csharp
            _roomRepository.Create(room);
```
→
```csharp
            await _roomRepository.Create(room);
```

`GetRoomMessages` (đã là `async Task<IActionResult>` từ Task 2) — thay dòng 50:
```csharp
            var room = _roomRepository.GetById(id);
```
→
```csharp
            var room = await _roomRepository.GetById(id);
```

- [ ] **Step 3: Build + test**

Run: `cd backend/NexivraChatBackend && dotnet build` → 0 errors.
Run: `cd backend/NexivraChatBackend.Tests && dotnet test` → Passed 8/8.

- [ ] **Step 4: Commit**

```bash
git add backend/NexivraChatBackend/Repositories/RoomRepository.cs backend/NexivraChatBackend/Controllers/RoomsController.cs
git commit -m "perf(db): async hóa RoomRepository và các caller"
```

---

### Task 5: Async PrivateChatRepository + tất cả caller

**Files:**
- Modify: `backend/NexivraChatBackend/Repositories/PrivateChatRepository.cs`
- Modify: `backend/NexivraChatBackend/Hubs/ChatHub.cs` (`SendPrivateMessage`, call `GetOrCreate` dòng 190)
- Modify: `backend/NexivraChatBackend/Controllers/UsersController.cs` (`GetOrCreatePrivateChat` dòng 50-66, call `GetById` trong `GetPrivateChatMessages` dòng 71)

**Interfaces:**
- Produces (PrivateChatRepository):
  - `Task<PrivateChat> GetOrCreate(int user1Id, int user2Id)`
  - `Task<PrivateChat?> GetById(int id)`

- [ ] **Step 1: Thay toàn bộ thân class `PrivateChatRepository`**

Ghi đè `backend/NexivraChatBackend/Repositories/PrivateChatRepository.cs` bằng:
```csharp
using Dapper;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
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

        public async Task<PrivateChat> GetOrCreate(int user1Id, int user2Id)
        {
            // Đảm bảo u1 < u2 để khớp với constraint UNIQUE (user1_id, user2_id)
            int u1 = Math.Min(user1Id, user2Id);
            int u2 = Math.Max(user1Id, user2Id);

            using (var connection = _context.CreateConnection())
            {
                var selectQuery = "SELECT id, user1_id AS User1Id, user2_id AS User2Id, created_at AS CreatedAt FROM private_chats WHERE user1_id = @u1 AND user2_id = @u2 LIMIT 1;";
                var chat = await connection.QueryFirstOrDefaultAsync<PrivateChat>(selectQuery, new { u1, u2 });
                if (chat != null)
                {
                    return chat;
                }

                var insertQuery = @"
                    INSERT INTO private_chats (user1_id, user2_id, created_at)
                    VALUES (@u1, @u2, @created_at)
                    RETURNING id, user1_id AS User1Id, user2_id AS User2Id, created_at AS CreatedAt;";

                return await connection.QuerySingleAsync<PrivateChat>(insertQuery, new
                {
                    u1,
                    u2,
                    created_at = DateTime.Now
                });
            }
        }

        public async Task<PrivateChat?> GetById(int id)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT id, user1_id AS User1Id, user2_id AS User2Id, created_at AS CreatedAt FROM private_chats WHERE id = @id LIMIT 1;";
                return await connection.QueryFirstOrDefaultAsync<PrivateChat>(query, new { id });
            }
        }
    }
}
```

- [ ] **Step 2: Await `GetOrCreate` trong `ChatHub.SendPrivateMessage`**

(`SendPrivateMessage` đã là `async Task`.) Dòng 190:
```csharp
            var privateChat = _privateChatRepository.GetOrCreate(senderId, receiverId);
```
→
```csharp
            var privateChat = await _privateChatRepository.GetOrCreate(senderId, receiverId);
```

- [ ] **Step 3: Chuyển `UsersController.GetOrCreatePrivateChat` sang async; await `GetById`**

`GetOrCreatePrivateChat` — thay signature (dòng 51):
```csharp
        public IActionResult GetOrCreatePrivateChat([FromBody] CreatePrivateChatDto dto)
```
→
```csharp
        public async Task<IActionResult> GetOrCreatePrivateChat([FromBody] CreatePrivateChatDto dto)
```
và thay dòng 64:
```csharp
            var privateChat = _privateChatRepository.GetOrCreate(currentUserId, dto.ReceiverId);
```
→
```csharp
            var privateChat = await _privateChatRepository.GetOrCreate(currentUserId, dto.ReceiverId);
```

`GetPrivateChatMessages` (đã `async Task<IActionResult>` từ Task 2) — thay dòng 71:
```csharp
            var chat = _privateChatRepository.GetById(id);
```
→
```csharp
            var chat = await _privateChatRepository.GetById(id);
```
(`using System.Threading.Tasks;` đã được thêm vào `UsersController.cs` ở Task 2/3 — xác nhận có; nếu chưa, thêm.)

- [ ] **Step 4: Build + test**

Run: `cd backend/NexivraChatBackend && dotnet build` → 0 errors.
Run: `cd backend/NexivraChatBackend.Tests && dotnet test` → Passed 8/8.

- [ ] **Step 5: Commit**

```bash
git add backend/NexivraChatBackend/Repositories/PrivateChatRepository.cs backend/NexivraChatBackend/Hubs/ChatHub.cs backend/NexivraChatBackend/Controllers/UsersController.cs
git commit -m "perf(db): async hóa PrivateChatRepository và các caller"
```

---

### Task 6: Async ProfileRepository + tất cả caller

**Files:**
- Modify: `backend/NexivraChatBackend/Repositories/ProfileRepository.cs`
- Modify: `backend/NexivraChatBackend/Controllers/ProfileController.cs` (`GetOwnProfile` dòng 36-69, `GetUserProfile` dòng 71-103, `UpdateProfile` dòng 105-147, `AnalyzeProfile` dòng 149-278 — các call `GetByUserId`/`Upsert`)

**Interfaces:**
- Produces (ProfileRepository):
  - `Task<UserProfile?> GetByUserId(int userId)`
  - `Task Upsert(UserProfile profile)`

- [ ] **Step 1: Thay toàn bộ thân class `ProfileRepository`**

Ghi đè `backend/NexivraChatBackend/Repositories/ProfileRepository.cs` bằng:
```csharp
using Dapper;
using System;
using System.Data;
using System.Threading.Tasks;
using NexivraChatBackend.Data;
using NexivraChatBackend.Models;

namespace NexivraChatBackend.Repositories
{
    public class ProfileRepository
    {
        private readonly DapperContext _context;

        public ProfileRepository(DapperContext context)
        {
            _context = context;
        }

        public async Task<UserProfile?> GetByUserId(int userId)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = "SELECT user_id, bio, native_language, ai_analysis_json, last_analyzed_at FROM user_profiles WHERE user_id = @userId LIMIT 1";
                return await connection.QueryFirstOrDefaultAsync<UserProfile>(query, new { userId });
            }
        }

        public async Task Upsert(UserProfile profile)
        {
            using (var connection = _context.CreateConnection())
            {
                var query = @"
                    INSERT INTO user_profiles (user_id, bio, native_language, ai_analysis_json, last_analyzed_at)
                    VALUES (@UserId, @Bio, @NativeLanguage, @AiAnalysisJson::jsonb, @LastAnalyzedAt)
                    ON CONFLICT (user_id)
                    DO UPDATE SET
                        bio = EXCLUDED.bio,
                        native_language = EXCLUDED.native_language,
                        ai_analysis_json = EXCLUDED.ai_analysis_json,
                        last_analyzed_at = EXCLUDED.last_analyzed_at;";

                await connection.ExecuteAsync(query, profile);
            }
        }
    }
}
```

- [ ] **Step 2: Chuyển `GetOwnProfile` + `UpdateProfile` sang async; await mọi call repo trong ProfileController**

`GetOwnProfile` — thay signature (dòng 37):
```csharp
        public IActionResult GetOwnProfile()
```
→
```csharp
        public async Task<IActionResult> GetOwnProfile()
```
và thay dòng 47:
```csharp
            var profile = _profileRepository.GetByUserId(userId);
```
→
```csharp
            var profile = await _profileRepository.GetByUserId(userId);
```

`GetUserProfile` (đã `async Task<IActionResult>` từ Task 3) — thay dòng 81:
```csharp
            var profile = _profileRepository.GetByUserId(userId);
```
→
```csharp
            var profile = await _profileRepository.GetByUserId(userId);
```

`UpdateProfile` — thay signature (dòng 106):
```csharp
        public IActionResult UpdateProfile([FromBody] UpdateProfileDto dto)
```
→
```csharp
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
```
và thay dòng 114:
```csharp
            var profile = _profileRepository.GetByUserId(userId);
```
→
```csharp
            var profile = await _profileRepository.GetByUserId(userId);
```
và thay dòng 135:
```csharp
            _profileRepository.Upsert(profile);
```
→
```csharp
            await _profileRepository.Upsert(profile);
```

`AnalyzeProfile` (đã `async Task<IActionResult>`) — thay dòng 249:
```csharp
            var profile = _profileRepository.GetByUserId(userId);
```
→
```csharp
            var profile = await _profileRepository.GetByUserId(userId);
```
và thay dòng 267:
```csharp
            _profileRepository.Upsert(profile);
```
→
```csharp
            await _profileRepository.Upsert(profile);
```

- [ ] **Step 3: Build + test (kiểm tra không còn call repo đồng bộ)**

Run: `cd backend/NexivraChatBackend && dotnet build` → 0 errors, không có warning CS1998.
Run: `cd backend/NexivraChatBackend.Tests && dotnet test` → Passed 8/8.
Run: `git grep -nE "_(message|user|room|privateChat|profile)Repository\.(Get|Save|Create|Upsert)" backend/NexivraChatBackend/ | grep -v await` → Expected: không có dòng nào (mọi call repo đều có `await`).

- [ ] **Step 4: Commit**

```bash
git add backend/NexivraChatBackend/Repositories/ProfileRepository.cs backend/NexivraChatBackend/Controllers/ProfileController.cs
git commit -m "perf(db): async hóa ProfileRepository và các caller"
```

---

## Self-Review (đã thực hiện)

- **Spec coverage:** GĐ2 trong spec gồm: async hóa Dapper toàn bộ repository (Task 2-6 phủ 5 repo: Message, User, Room, PrivateChat, Profile); cập nhật caller ChatHub + Controllers (đã liệt kê từng call site theo từng task); IHttpClientFactory cho AiService + TranslationService (Task 1). Đủ.
- **Placeholder scan:** Không có TBD/TODO; mọi step có code/lệnh cụ thể.
- **Type consistency:** Các chữ ký async (`Task<List<Message>>`, `Task<User?>`, `Task<ChatRoom?>`, `Task<PrivateChat>`, `Task<UserProfile?>`, `Task SaveNewMessage/Create/Upsert`) khớp giữa phần định nghĩa repo và phần await ở caller. Mọi method gán Id (SaveNewMessage/Create) trả `Task` và vẫn gán Id qua side-effect.
- **Build-green giữa các task:** Mỗi task convert trọn vẹn 1 repo + tất cả caller của nó trong cùng commit. Controller dùng nhiều repo (Users/Profile/Rooms) chỉ chuyển async ở method liên quan; các call tới repo chưa convert vẫn đồng bộ và build được. Method controller chuyển async ở task sớm (vd `GetRoomMessages`, `GetPrivateChatMessages`, `GetUserProfile`) được task sau tái sử dụng (chỉ thêm `await`).
- **Ranh giới phạm vi:** `DbInitializer` giữ đồng bộ (startup, không phải repository); nội dung SQL không đổi (đã tường minh cột từ GĐ1); không đụng frontend (thuộc GĐ3).
- **Lưu ý reviewer:** kiểm tra không có `.Result`/`.Wait()`/`.GetAwaiter().GetResult()`/`Task.Run` được thêm; mọi method `async` đều thực sự `await` (không có CS1998).
