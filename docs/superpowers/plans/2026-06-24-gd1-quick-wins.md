# Giai đoạn 1 — Quick Wins (Tối ưu hiệu năng) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Áp dụng các tối ưu hiệu năng nhỏ, độc lập, rủi ro thấp — không đụng kiến trúc — cho NexivraChat.

**Architecture:** Bốn thay đổi tách biệt: (1) thêm index DB trong `DbInitializer`, (2) thay `SELECT *` bằng cột tường minh trong các repository, (3) thay `new Random()` sinh temp-ID âm bằng helper `Interlocked` có thể unit-test, (4) sửa auto-scroll frontend để hết giật khi AI stream. Mỗi thay đổi commit riêng.

**Tech Stack:** .NET 8, Dapper, PostgreSQL (Npgsql), xUnit; React 19 + TypeScript + Vite.

## Global Constraints

- Database: PostgreSQL + Dapper SQL thuần — **tuyệt đối không dùng EF Core**.
- Dapper map snake_case ↔ PascalCase tự động (`DefaultTypeMap.MatchNamesWithUnderscores = true` ở `DapperContext`). Cột SQL viết **snake_case**.
- Không thêm tính năng mới, không refactor ngoài phạm vi hiệu năng.
- Backend test framework: xUnit (project `NexivraChatBackend.Tests`).
- Baseline hiện tại được coi là ổn định, chạy được.

---

### Task 1: Thêm index DB trong DbInitializer

Index DDL không unit-test được không có DB tích hợp; kiểm chứng bằng build + chạy app + `EXPLAIN`.

**Files:**
- Modify: `backend/NexivraChatBackend/Data/DbInitializer.cs`

**Interfaces:**
- Consumes: hàm có sẵn `DbInitializer.Initialize(DapperContext context)`.
- Produces: (không có interface mới — chỉ thêm câu lệnh DDL idempotent).

- [ ] **Step 1: Thêm các lệnh tạo index sau khi tạo bảng `messages`**

Trong `DbInitializer.Initialize`, ngay **sau** dòng `connection.Execute(createUserProfilesTable);` (hiện ở dòng 67), thêm:

```csharp
                // 6. Tạo index tối ưu truy vấn lịch sử tin nhắn (idempotent)
                var createMessageIndexes = @"
                    CREATE INDEX IF NOT EXISTS idx_messages_room_created
                        ON messages (room_id, created_at);
                    CREATE INDEX IF NOT EXISTS idx_messages_private_created
                        ON messages (private_chat_id, created_at);
                    CREATE INDEX IF NOT EXISTS idx_messages_sender
                        ON messages (sender_name);";
                connection.Execute(createMessageIndexes);
```

- [ ] **Step 2: Build backend**

Run: `cd backend && dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Chạy app và xác nhận khởi tạo DB thành công**

Run: `cd backend/NexivraChatBackend && dotnet run`
Expected: console in `Database initialized successfully.` không có exception. (Dừng app sau khi thấy log — Ctrl+C.)

- [ ] **Step 4: Xác minh index tồn tại (qua psql hoặc bất kỳ client PostgreSQL)**

Run (psql): `\di idx_messages_*`
Expected: thấy `idx_messages_room_created`, `idx_messages_private_created`, `idx_messages_sender`.
(Nếu không có psql, chạy `EXPLAIN SELECT * FROM messages WHERE room_id = 1 ORDER BY created_at DESC LIMIT 50;` và xác nhận có `Index Scan`/`Bitmap Index Scan` trên `idx_messages_room_created`.)

- [ ] **Step 5: Commit**

```bash
git add backend/NexivraChatBackend/Data/DbInitializer.cs
git commit -m "perf(db): thêm index messages cho room/private/sender"
```

---

### Task 2: Thay `SELECT *` bằng cột tường minh trong repository

Thay đổi chuỗi SQL; kiểm chứng bằng build + chạy app + xác nhận dữ liệu load đúng. Cột phải khớp model (snake_case).

**Files:**
- Modify: `backend/NexivraChatBackend/Repositories/MessageRepository.cs` (4 query: dòng 24, 33, 44, 77)
- Modify: `backend/NexivraChatBackend/Repositories/UserRepository.cs` (1 query: dòng 21)
- Modify: `backend/NexivraChatBackend/Repositories/RoomRepository.cs` (2 query: dòng 22, 31)
- Modify: `backend/NexivraChatBackend/Repositories/ProfileRepository.cs` (1 query: dòng 22)

**Interfaces:**
- Consumes: model `Message` (id, room_id, private_chat_id, sender_name, content, created_at, is_ai), `User` (id, username, password_hash, created_at), `ChatRoom` (id, name, description), `UserProfile` (user_id, bio, native_language, ai_analysis_json, last_analyzed_at).
- Produces: (không đổi chữ ký — chỉ đổi nội dung SQL).

- [ ] **Step 1: Sửa `MessageRepository` — thay cả 4 `SELECT *` bằng danh sách cột**

Cột messages dùng cho mọi query: `id, room_id, private_chat_id, sender_name, content, created_at, is_ai`.

`GetOldMessages` (dòng 24):
```csharp
                var query = "SELECT id, room_id, private_chat_id, sender_name, content, created_at, is_ai FROM messages ORDER BY created_at DESC LIMIT @limit OFFSET @offset";
```
`GetMessagesByRoom` (dòng 33):
```csharp
                var query = "SELECT id, room_id, private_chat_id, sender_name, content, created_at, is_ai FROM messages WHERE room_id = @roomId ORDER BY created_at DESC LIMIT @limit OFFSET @offset";
```
`GetMessagesByPrivateChat` (dòng 44):
```csharp
                var query = "SELECT id, room_id, private_chat_id, sender_name, content, created_at, is_ai FROM messages WHERE private_chat_id = @privateChatId ORDER BY created_at DESC LIMIT @limit OFFSET @offset";
```
`GetLatestMessagesBySender` (dòng 77):
```csharp
                var query = "SELECT id, room_id, private_chat_id, sender_name, content, created_at, is_ai FROM messages WHERE sender_name = @senderName AND is_ai = false ORDER BY created_at DESC LIMIT @limit";
```

- [ ] **Step 2: Sửa `UserRepository.GetByUsername` (dòng 21)**

Cần `password_hash` để xác thực đăng nhập nên giữ trong danh sách cột:
```csharp
                var query = "SELECT id, username, password_hash, created_at FROM users WHERE username = @username LIMIT 1";
```

- [ ] **Step 3: Sửa `RoomRepository` (dòng 22 và 31)**

`GetAll`:
```csharp
                var query = "SELECT id, name, description FROM chat_rooms ORDER BY name ASC";
```
`GetById`:
```csharp
                var query = "SELECT id, name, description FROM chat_rooms WHERE id = @id LIMIT 1";
```

- [ ] **Step 4: Sửa `ProfileRepository.GetByUserId` (dòng 22)**

```csharp
                var query = "SELECT user_id, bio, native_language, ai_analysis_json, last_analyzed_at FROM user_profiles WHERE user_id = @userId LIMIT 1";
```

- [ ] **Step 5: Build backend**

Run: `cd backend && dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Chạy app + smoke test thủ công**

Run: `cd backend/NexivraChatBackend && dotnet run` (kèm frontend nếu cần).
Expected: đăng nhập được, danh sách phòng hiển thị, lịch sử tin nhắn load đúng, gửi tin/`@copilot`/chat 1-1 hoạt động, mở profile load đúng. (Xác nhận không có cột nào bị null bất thường do sai tên cột.)

- [ ] **Step 7: Commit**

```bash
git add backend/NexivraChatBackend/Repositories/MessageRepository.cs backend/NexivraChatBackend/Repositories/UserRepository.cs backend/NexivraChatBackend/Repositories/RoomRepository.cs backend/NexivraChatBackend/Repositories/ProfileRepository.cs
git commit -m "perf(db): thay SELECT * bằng cột tường minh trong repository"
```

---

### Task 3: Thay `new Random()` temp-ID bằng helper `Interlocked` có unit test

Tách logic sinh temp-ID âm ra helper tĩnh để unit-test được tính duy nhất; thay chỗ dùng trong `ChatHub`.

**Files:**
- Create: `backend/NexivraChatBackend/Services/TempMessageId.cs`
- Test: `backend/NexivraChatBackend.Tests/TempMessageIdTests.cs`
- Modify: `backend/NexivraChatBackend/Hubs/ChatHub.cs:132`

**Interfaces:**
- Produces: `static class NexivraChatBackend.Services.TempMessageId { static int Next() }` — trả về int âm (< 0), khác nhau qua mỗi lần gọi trong cùng tiến trình.
- Consumes (trong ChatHub): thay biểu thức sinh `tempAiMessageId`.

- [ ] **Step 1: Viết test thất bại**

Tạo `backend/NexivraChatBackend.Tests/TempMessageIdTests.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NexivraChatBackend.Services;
using Xunit;

namespace NexivraChatBackend.Tests
{
    public class TempMessageIdTests
    {
        [Fact]
        public void Next_ReturnsNegativeValue()
        {
            Assert.True(TempMessageId.Next() < 0);
        }

        [Fact]
        public void Next_IsUniqueAcrossConcurrentCalls()
        {
            var results = new System.Collections.Concurrent.ConcurrentBag<int>();
            Parallel.For(0, 1000, _ => results.Add(TempMessageId.Next()));
            Assert.Equal(1000, results.Distinct().Count());
        }
    }
}
```

- [ ] **Step 2: Chạy test để xác nhận FAIL**

Run: `cd backend && dotnet test --filter "FullyQualifiedName~TempMessageIdTests"`
Expected: FAIL — không build được vì `TempMessageId` chưa tồn tại.

- [ ] **Step 3: Viết implementation tối thiểu**

Tạo `backend/NexivraChatBackend/Services/TempMessageId.cs`:
```csharp
using System.Threading;

namespace NexivraChatBackend.Services
{
    // Sinh ID tạm thời (âm) duy nhất cho tin nhắn AI đang stream,
    // tránh trùng và tránh cấp phát Random mỗi lần như trước.
    public static class TempMessageId
    {
        private static int _counter = 0;

        public static int Next()
        {
            return Interlocked.Decrement(ref _counter);
        }
    }
}
```

- [ ] **Step 4: Chạy test để xác nhận PASS**

Run: `cd backend && dotnet test --filter "FullyQualifiedName~TempMessageIdTests"`
Expected: PASS (2 test).

- [ ] **Step 5: Dùng helper trong `ChatHub`**

Trong `backend/NexivraChatBackend/Hubs/ChatHub.cs` dòng 132, thay:
```csharp
                var tempAiMessageId = -1 * new Random().Next(1, 1000000);
```
bằng:
```csharp
                var tempAiMessageId = TempMessageId.Next();
```
(`ChatHub` đã có `using NexivraChatBackend.Services;` ở đầu file — không cần thêm.)

- [ ] **Step 6: Build + chạy toàn bộ test**

Run: `cd backend && dotnet build && dotnet test`
Expected: Build succeeded; tất cả test (PresenceTracker + TempMessageId) PASS.

- [ ] **Step 7: Commit**

```bash
git add backend/NexivraChatBackend/Services/TempMessageId.cs backend/NexivraChatBackend.Tests/TempMessageIdTests.cs backend/NexivraChatBackend/Hubs/ChatHub.cs
git commit -m "perf(hub): thay new Random() temp-ID bằng Interlocked helper"
```

---

### Task 4: Sửa auto-scroll frontend để hết giật khi AI stream

Hiện `useEffect([messages])` gọi `scrollIntoView({behavior:'smooth'})` mỗi token AI → smooth-scroll liên tục gây giật. Dùng `'smooth'` cho tin nhắn mới bình thường, `'auto'` khi đang stream (mảng `messages` đổi nhưng số phần tử không đổi — chỉ nội dung bubble cuối cập nhật).

**Files:**
- Modify: `frontend/nexivra-chat-frontend/src/views/ChatView.tsx` (effect dòng 287-289)

**Interfaces:**
- Consumes: state `messages` (`Message[]`), ref `messageEndRef`.
- Produces: (không có — chỉ đổi hành vi effect).

- [ ] **Step 1: Thêm ref theo dõi số lượng tin nhắn trước đó**

Ngay sau khai báo `const messageEndRef = useRef<HTMLDivElement>(null);` (dòng 64), thêm:
```tsx
  const prevMessageCountRef = useRef(0);
```

- [ ] **Step 2: Thay effect auto-scroll (dòng 287-289)**

Thay:
```tsx
  // Tự động cuộn xuống dưới khi có tin nhắn mới
  useEffect(() => {
    messageEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);
```
bằng:
```tsx
  // Tự động cuộn xuống dưới khi có tin nhắn mới.
  // Tin nhắn MỚI (số lượng tăng) -> cuộn mượt. Đang stream token AI
  // (số lượng không đổi, chỉ nội dung bubble cuối cập nhật) -> cuộn tức thì
  // để tránh hiệu ứng smooth chạy liên tục gây giật.
  useEffect(() => {
    const isNewMessage = messages.length !== prevMessageCountRef.current;
    prevMessageCountRef.current = messages.length;
    messageEndRef.current?.scrollIntoView({ behavior: isNewMessage ? 'smooth' : 'auto' });
  }, [messages]);
```

- [ ] **Step 3: Build frontend**

Run: `cd frontend/nexivra-chat-frontend && npm run build`
Expected: build thành công, 0 lỗi TypeScript.

- [ ] **Step 4: Smoke test thủ công khi AI stream**

Run: `cd frontend/nexivra-chat-frontend && npm run dev` (kèm backend chạy).
Expected: gửi `@copilot <câu hỏi dài>`; khung chat cuộn theo mượt, không giật/nhảy liên tục trong lúc token đổ về; tin nhắn mới (người dùng/AI placeholder) vẫn cuộn mượt.

- [ ] **Step 5: Commit**

```bash
git add frontend/nexivra-chat-frontend/src/views/ChatView.tsx
git commit -m "perf(ui): chỉ smooth-scroll tin nhắn mới, dùng auto khi AI stream"
```

---

## Self-Review (đã thực hiện)

- **Spec coverage:** GĐ1 trong spec gồm 4 hạng mục — index DB (Task 1), bỏ `SELECT *` (Task 2), thay `new Random()` (Task 3), sửa auto-scroll (Task 4). Đủ.
- **Placeholder scan:** Không có TBD/TODO; mọi step có code/lệnh cụ thể.
- **Type consistency:** `TempMessageId.Next()` dùng nhất quán giữa test, implementation, và `ChatHub`. Danh sách cột SQL khớp model.
- **Lưu ý phạm vi:** `GetByUsername` giữ `password_hash` (cần cho auth); `GetAll` của `UserRepository` đã tường minh sẵn — không đụng. Async hóa Dapper và IHttpClientFactory thuộc GĐ2, không nằm trong plan này.
