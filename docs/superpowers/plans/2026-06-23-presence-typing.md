# Presence & Typing Indicator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sửa nền tảng cho chạy ổn end-to-end và thêm tính năng Presence (ai đang online trong phòng) + Typing indicator ("đang gõ...") dạng in-memory qua SignalR.

**Architecture:** Một service singleton `PresenceTracker` giữ trạng thái "ai đang ở phòng nào" trong bộ nhớ server (đếm theo connectionId để xử lý nhiều tab). `ChatHub` cập nhật tracker khi join/leave/disconnect và phát `PresenceUpdate`/`TypingUpdate` tới phòng. Frontend `ChatView` lắng nghe và hiển thị, gửi sự kiện `Typing` có debounce. Không đụng schema DB.

**Tech Stack:** .NET 8, SignalR, xUnit (test project mới), React 19 + TypeScript, antd, @microsoft/signalr.

## Global Constraints

- Database: Dapper thuần, KHÔNG dùng EF Core.
- UI: antd, theme terminal tối, KHÔNG dùng màu tím; màu nhấn `#a3e635`, font `monospace`.
- Presence/Typing: in-memory, KHÔNG đổi schema DB, KHÔNG migration.
- Phạm vi presence: chỉ phòng đang mở.
- API key Gemini KHÔNG được commit lên Git.
- Backend project: `backend/NexivraChatBackend/NexivraChatBackend.csproj`.
- Hub path: `/chatHub`. Username lấy từ `Context.User?.Identity?.Name` (JWT).
- Sau mỗi thay đổi code phải cập nhật `context.md` ở gốc repo (Task 6).

---

### Task 1: Foundation — xác minh & cấu hình an toàn key Gemini

**Files:**
- Verify (no change expected): `frontend/nexivra-chat-frontend/src/components/CopilotPanel.tsx`
- Create: `backend/NexivraChatBackend/appsettings.Development.json`
- Modify: `.gitignore` (gốc repo — tạo nếu chưa có)
- Verify: `backend/NexivraChatBackend/appsettings.json` (đã có `"Gemini": { "ApiKey": "" }`)

**Interfaces:**
- Consumes: `AiService` đọc key qua `config["Gemini:ApiKey"]` rồi fallback `GEMINI_API_KEY` (đã tồn tại, không sửa).
- Produces: key thật nằm trong `appsettings.Development.json` (gitignored); mock mode khi không có key.

- [ ] **Step 1: Xác minh CopilotPanel không còn lỗi icon**

Mở `frontend/nexivra-chat-frontend/src/components/CopilotPanel.tsx`. Xác nhận dòng import có `BulbOutlined` và KHÔNG còn `<LightBulbOutlined />` trong file.

Run: `grep -n "LightBulbOutlined" frontend/nexivra-chat-frontend/src/components/CopilotPanel.tsx`
Expected: không có kết quả (lỗi đã được sửa từ trước; context.md cũ).

- [ ] **Step 2: Cài đặt thư viện frontend**

Run (PowerShell, trong thư mục frontend):
```
cd frontend/nexivra-chat-frontend; npm install
```
Expected: tạo `node_modules`, kết thúc không lỗi.

- [ ] **Step 3: Tạo file cấu hình Development chứa key thật**

Create `backend/NexivraChatBackend/appsettings.Development.json`:
```json
{
  "Gemini": {
    "ApiKey": "<GEMINI_API_KEY — lấy từ người dùng, KHÔNG commit>"
  }
}
```
> Key thật do người dùng cung cấp riêng tại thời điểm thực thi; tuyệt đối không ghi key vào bất kỳ file nào được Git theo dõi (kể cả plan này).

- [ ] **Step 4: Bảo vệ key khỏi Git**

Thêm dòng sau vào `.gitignore` ở gốc repo (tạo file nếu chưa có), kèm các mục build .NET/Node thông dụng nếu chưa có:
```
# Secrets - local dev config (chứa API key)
appsettings.Development.json
backend/NexivraChatBackend/appsettings.Development.json

# Node
node_modules/

# .NET build output
bin/
obj/
```

- [ ] **Step 5: Xác minh key không bị Git theo dõi**

Run: `git status --porcelain backend/NexivraChatBackend/appsettings.Development.json`
Expected: KHÔNG có dòng nào (file bị ignore).

- [ ] **Step 6: Commit**

```bash
git add .gitignore backend/NexivraChatBackend/appsettings.json
git commit -m "chore: gitignore dev secrets and verify foundation fixes"
```

---

### Task 2: PresenceTracker (logic thuần, TDD bằng xUnit)

**Files:**
- Create: `backend/NexivraChatBackend/Services/PresenceTracker.cs`
- Create test project: `backend/NexivraChatBackend.Tests/NexivraChatBackend.Tests.csproj`
- Create: `backend/NexivraChatBackend.Tests/PresenceTrackerTests.cs`

**Interfaces:**
- Produces (lớp `NexivraChatBackend.Services.PresenceTracker`, dùng ở Task 3):
  - `void UserJoined(int roomId, string connectionId, string username)`
  - `void UserLeft(int roomId, string connectionId, string username)`
  - `int[] RemoveConnection(string connectionId)` — gỡ connection khỏi MỌI phòng, trả về mảng `roomId` bị ảnh hưởng
  - `string[] GetOnlineUsers(int roomId)` — danh sách username distinct đang online, sắp xếp tăng dần

- [ ] **Step 1: Tạo test project xUnit và tham chiếu project chính**

Run (PowerShell, ở gốc repo):
```
dotnet new xunit -o backend/NexivraChatBackend.Tests
dotnet add backend/NexivraChatBackend.Tests/NexivraChatBackend.Tests.csproj reference backend/NexivraChatBackend/NexivraChatBackend.csproj
```
Expected: tạo project test, thêm reference thành công. Xóa file mẫu `UnitTest1.cs` nếu có:
```
Remove-Item backend/NexivraChatBackend.Tests/UnitTest1.cs -ErrorAction SilentlyContinue
```

- [ ] **Step 2: Viết test thất bại**

Create `backend/NexivraChatBackend.Tests/PresenceTrackerTests.cs`:
```csharp
using NexivraChatBackend.Services;
using Xunit;

namespace NexivraChatBackend.Tests
{
    public class PresenceTrackerTests
    {
        [Fact]
        public void GetOnlineUsers_EmptyRoom_ReturnsEmpty()
        {
            var tracker = new PresenceTracker();
            Assert.Empty(tracker.GetOnlineUsers(1));
        }

        [Fact]
        public void UserJoined_AddsUserToRoom()
        {
            var tracker = new PresenceTracker();
            tracker.UserJoined(1, "conn-a", "alice");
            Assert.Equal(new[] { "alice" }, tracker.GetOnlineUsers(1));
        }

        [Fact]
        public void GetOnlineUsers_ReturnsDistinctSorted()
        {
            var tracker = new PresenceTracker();
            tracker.UserJoined(1, "conn-b", "bob");
            tracker.UserJoined(1, "conn-a", "alice");
            Assert.Equal(new[] { "alice", "bob" }, tracker.GetOnlineUsers(1));
        }

        [Fact]
        public void MultipleConnections_SameUser_CountsOnceUntilAllLeft()
        {
            var tracker = new PresenceTracker();
            tracker.UserJoined(1, "conn-1", "alice");
            tracker.UserJoined(1, "conn-2", "alice"); // alice mở 2 tab
            Assert.Equal(new[] { "alice" }, tracker.GetOnlineUsers(1));

            tracker.UserLeft(1, "conn-1", "alice"); // đóng 1 tab
            Assert.Equal(new[] { "alice" }, tracker.GetOnlineUsers(1)); // vẫn online

            tracker.UserLeft(1, "conn-2", "alice"); // đóng nốt tab còn lại
            Assert.Empty(tracker.GetOnlineUsers(1)); // giờ mới offline
        }

        [Fact]
        public void RemoveConnection_RemovesFromAllRooms_ReturnsAffectedRoomIds()
        {
            var tracker = new PresenceTracker();
            tracker.UserJoined(1, "conn-x", "alice");
            tracker.UserJoined(2, "conn-x", "alice"); // cùng connection ở 2 phòng

            var affected = tracker.RemoveConnection("conn-x");

            Assert.Equal(new[] { 1, 2 }, affected.OrderBy(r => r).ToArray());
            Assert.Empty(tracker.GetOnlineUsers(1));
            Assert.Empty(tracker.GetOnlineUsers(2));
        }

        [Fact]
        public void UserLeft_UnknownConnection_DoesNotThrow()
        {
            var tracker = new PresenceTracker();
            tracker.UserLeft(1, "ghost", "nobody"); // không ném lỗi
            Assert.Empty(tracker.GetOnlineUsers(1));
        }
    }
}
```

- [ ] **Step 3: Chạy test để xác nhận FAIL (chưa có lớp)**

Run: `dotnet test backend/NexivraChatBackend.Tests/NexivraChatBackend.Tests.csproj`
Expected: build FAIL — `PresenceTracker` chưa tồn tại.

- [ ] **Step 4: Hiện thực `PresenceTracker`**

Create `backend/NexivraChatBackend/Services/PresenceTracker.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;

namespace NexivraChatBackend.Services
{
    /// <summary>
    /// Theo dõi presence trong bộ nhớ: phòng nào có những connection/username nào đang online.
    /// Một user có thể mở nhiều tab (nhiều connectionId); chỉ offline khi tất cả connection rời đi.
    /// An toàn đa luồng nhờ lock toàn cục.
    /// </summary>
    public class PresenceTracker
    {
        private readonly object _lock = new object();
        // roomId -> (connectionId -> username)
        private readonly Dictionary<int, Dictionary<string, string>> _rooms
            = new Dictionary<int, Dictionary<string, string>>();

        public void UserJoined(int roomId, string connectionId, string username)
        {
            lock (_lock)
            {
                if (!_rooms.TryGetValue(roomId, out var conns))
                {
                    conns = new Dictionary<string, string>();
                    _rooms[roomId] = conns;
                }
                conns[connectionId] = username;
            }
        }

        public void UserLeft(int roomId, string connectionId, string username)
        {
            lock (_lock)
            {
                if (_rooms.TryGetValue(roomId, out var conns))
                {
                    conns.Remove(connectionId);
                    if (conns.Count == 0)
                    {
                        _rooms.Remove(roomId);
                    }
                }
            }
        }

        public int[] RemoveConnection(string connectionId)
        {
            lock (_lock)
            {
                var affected = new List<int>();
                foreach (var roomId in _rooms.Keys.ToList())
                {
                    var conns = _rooms[roomId];
                    if (conns.Remove(connectionId))
                    {
                        affected.Add(roomId);
                        if (conns.Count == 0)
                        {
                            _rooms.Remove(roomId);
                        }
                    }
                }
                return affected.ToArray();
            }
        }

        public string[] GetOnlineUsers(int roomId)
        {
            lock (_lock)
            {
                if (_rooms.TryGetValue(roomId, out var conns))
                {
                    return conns.Values.Distinct().OrderBy(u => u).ToArray();
                }
                return new string[0];
            }
        }
    }
}
```

- [ ] **Step 5: Chạy test để xác nhận PASS**

Run: `dotnet test backend/NexivraChatBackend.Tests/NexivraChatBackend.Tests.csproj`
Expected: tất cả 6 test PASS.

- [ ] **Step 6: Commit**

```bash
git add backend/NexivraChatBackend/Services/PresenceTracker.cs backend/NexivraChatBackend.Tests
git commit -m "feat: add in-memory PresenceTracker with unit tests"
```

---

### Task 3: ChatHub — wiring presence + typing, đăng ký DI

**Files:**
- Modify: `backend/NexivraChatBackend/Hubs/ChatHub.cs`
- Modify: `backend/NexivraChatBackend/Program.cs:13` (sau `AddSignalR()`)

**Interfaces:**
- Consumes: `PresenceTracker` (Task 2) — các method `UserJoined/UserLeft/RemoveConnection/GetOnlineUsers`.
- Produces (sự kiện SignalR mà frontend Task 4 lắng nghe):
  - `PresenceUpdate(int roomId, string[] usernames)`
  - `TypingUpdate(int roomId, string username, bool isTyping)`
  - Hub method client gọi: `Typing(int roomId, bool isTyping)`

- [ ] **Step 1: Đăng ký PresenceTracker là singleton**

Modify `backend/NexivraChatBackend/Program.cs`, ngay sau dòng `builder.Services.AddSignalR();`:
```csharp
builder.Services.AddSignalR();
builder.Services.AddSingleton<NexivraChatBackend.Services.PresenceTracker>();
```

- [ ] **Step 2: Inject PresenceTracker vào ChatHub**

Modify `backend/NexivraChatBackend/Hubs/ChatHub.cs` — phần field + constructor:
```csharp
        private readonly MessageRepository _messageRepository;
        private readonly AiService _aiService;
        private readonly PresenceTracker _presenceTracker;

        public ChatHub(MessageRepository messageRepository, AiService aiService, PresenceTracker presenceTracker)
        {
            _messageRepository = messageRepository;
            _aiService = aiService;
            _presenceTracker = presenceTracker;
        }
```

- [ ] **Step 3: Cập nhật JoinRoom — ghi presence + phát PresenceUpdate**

Thay thế method `JoinRoom` trong `ChatHub.cs`:
```csharp
        public async Task JoinRoom(int roomId)
        {
            var roomString = roomId.ToString();
            await Groups.AddToGroupAsync(Context.ConnectionId, roomString);

            var username = Context.User?.Identity?.Name ?? "Ẩn danh";
            _presenceTracker.UserJoined(roomId, Context.ConnectionId, username);

            await Clients.Group(roomString).SendAsync("ReceiveNotification", $"{username} đã tham gia phòng.");
            await Clients.Group(roomString).SendAsync("PresenceUpdate", roomId, _presenceTracker.GetOnlineUsers(roomId));
        }
```

- [ ] **Step 4: Cập nhật LeaveRoom — xóa presence + phát PresenceUpdate + tắt typing**

Thay thế method `LeaveRoom` trong `ChatHub.cs`:
```csharp
        public async Task LeaveRoom(int roomId)
        {
            var roomString = roomId.ToString();
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomString);

            var username = Context.User?.Identity?.Name ?? "Ẩn danh";
            _presenceTracker.UserLeft(roomId, Context.ConnectionId, username);

            await Clients.Group(roomString).SendAsync("ReceiveNotification", $"{username} đã rời phòng.");
            await Clients.Group(roomString).SendAsync("PresenceUpdate", roomId, _presenceTracker.GetOnlineUsers(roomId));
            // Đảm bảo người khác không thấy "đang gõ" treo lại sau khi rời phòng
            await Clients.OthersInGroup(roomString).SendAsync("TypingUpdate", roomId, username, false);
        }
```

- [ ] **Step 5: Thêm method Typing**

Thêm method sau vào `ChatHub.cs` (ví dụ ngay sau `LeaveRoom`):
```csharp
        public async Task Typing(int roomId, bool isTyping)
        {
            var username = Context.User?.Identity?.Name ?? "Ẩn danh";
            var roomString = roomId.ToString();
            // Chỉ gửi cho người khác trong phòng, không gửi lại cho chính người gõ
            await Clients.OthersInGroup(roomString).SendAsync("TypingUpdate", roomId, username, isTyping);
        }
```

- [ ] **Step 6: Thêm OnDisconnectedAsync — dọn presence khi rớt kết nối**

Thêm method sau vào `ChatHub.cs`:
```csharp
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var username = Context.User?.Identity?.Name ?? "Ẩn danh";
            var affectedRooms = _presenceTracker.RemoveConnection(Context.ConnectionId);
            foreach (var roomId in affectedRooms)
            {
                var roomString = roomId.ToString();
                await Clients.Group(roomString).SendAsync("PresenceUpdate", roomId, _presenceTracker.GetOnlineUsers(roomId));
                await Clients.OthersInGroup(roomString).SendAsync("TypingUpdate", roomId, username, false);
            }
            await base.OnDisconnectedAsync(exception);
        }
```

Lưu ý: cần `using System;` (đã có sẵn trong file cho `Exception`).

- [ ] **Step 7: Build backend để xác nhận biên dịch sạch**

Run: `dotnet build backend/NexivraChatBackend/NexivraChatBackend.csproj`
Expected: Build succeeded, 0 lỗi.

- [ ] **Step 8: Commit**

```bash
git add backend/NexivraChatBackend/Hubs/ChatHub.cs backend/NexivraChatBackend/Program.cs
git commit -m "feat: wire presence + typing events into ChatHub"
```

---

### Task 4: Frontend — hiển thị Presence + Typing trong ChatView

**Files:**
- Modify: `frontend/nexivra-chat-frontend/src/views/ChatView.tsx`

**Interfaces:**
- Consumes (từ Hub Task 3): `PresenceUpdate(roomId, usernames)`, `TypingUpdate(roomId, username, isTyping)`; gọi `connection.invoke('Typing', roomId, isTyping)`.

- [ ] **Step 1: Thêm state cho online users, typing users và ref debounce**

Trong `ChatView.tsx`, sau dòng `const [notifications, setNotifications] = useState<string[]>([]);` thêm:
```tsx
  const [onlineUsers, setOnlineUsers] = useState<string[]>([]);
  const [typingUsers, setTypingUsers] = useState<string[]>([]);
  const typingTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const isTypingRef = useRef(false);
```

- [ ] **Step 2: Đăng ký listener PresenceUpdate và TypingUpdate**

Trong effect khởi tạo listener (block `if (connection) { ... }`), thêm ngay sau listener `connection.on('ReceiveNotification', ...)`:
```tsx
      // Cập nhật danh sách user đang online trong phòng
      connection.on('PresenceUpdate', (roomId: number, usernames: string[]) => {
        if (roomId === activeRoomId) {
          setOnlineUsers(usernames);
        }
      });

      // Cập nhật danh sách user đang gõ
      connection.on('TypingUpdate', (roomId: number, user: string, isTyping: boolean) => {
        if (roomId !== activeRoomId || user === username) return;
        setTypingUsers((prev) => {
          if (isTyping) {
            return prev.includes(user) ? prev : [...prev, user];
          }
          return prev.filter((u) => u !== user);
        });
      });
```

- [ ] **Step 3: Bổ sung cleanup cho 2 listener mới**

Trong khối `return () => { ... }` của cùng effect, thêm sau `connection.off('ReceiveNotification');`:
```tsx
        connection.off('PresenceUpdate');
        connection.off('TypingUpdate');
```

- [ ] **Step 4: Reset presence/typing khi đổi phòng**

Trong effect `useEffect(() => { ... }, [activeRoomId])` (block tải lịch sử + JoinRoom), thêm vào đầu block `if (activeRoomId !== null) {`:
```tsx
      setOnlineUsers([]);
      setTypingUsers([]);
```

- [ ] **Step 5: Hàm gửi typing có debounce**

Thêm hàm sau vào component (trên `handleSendMessage`):
```tsx
  const sendTyping = (isTyping: boolean) => {
    if (!connection || connection.state !== 'Connected' || activeRoomId === null) return;
    connection.invoke('Typing', activeRoomId, isTyping).catch(() => {});
  };

  const handleInputChange = (value: string) => {
    setInputText(value);
    if (!isTypingRef.current) {
      isTypingRef.current = true;
      sendTyping(true);
    }
    if (typingTimeoutRef.current) clearTimeout(typingTimeoutRef.current);
    typingTimeoutRef.current = setTimeout(() => {
      isTypingRef.current = false;
      sendTyping(false);
    }, 2000);
  };
```

- [ ] **Step 6: Tắt typing ngay khi gửi tin nhắn**

Trong `handleSendMessage`, ngay sau `await connection.invoke('SendMessage', activeRoomId, text.trim());` thêm:
```tsx
      if (typingTimeoutRef.current) clearTimeout(typingTimeoutRef.current);
      isTypingRef.current = false;
      sendTyping(false);
```

- [ ] **Step 7: Nối ô Input vào handleInputChange**

Trong JSX ô nhập tin nhắn, đổi:
```tsx
            onChange={(e) => setInputText(e.target.value)}
```
thành:
```tsx
            onChange={(e) => handleInputChange(e.target.value)}
```

- [ ] **Step 8: Hiển thị số online ở Room Header**

Trong Room Header, thay khối mô tả phòng:
```tsx
            <div style={{ fontSize: '11px', color: '#64748b', fontFamily: 'monospace', marginTop: '2px' }}>
              {activeRoom ? activeRoom.description : 'Please select a room to start discussions.'}
            </div>
```
thành:
```tsx
            <div style={{ fontSize: '11px', color: '#64748b', fontFamily: 'monospace', marginTop: '2px' }}>
              {activeRoom ? activeRoom.description : 'Please select a room to start discussions.'}
            </div>
            {activeRoom && (
              <div
                style={{ fontSize: '11px', color: '#a3e635', fontFamily: 'monospace', marginTop: '2px' }}
                title={onlineUsers.join(', ')}
              >
                ● {onlineUsers.length} online{onlineUsers.length > 0 ? `: ${onlineUsers.join(', ')}` : ''}
              </div>
            )}
```

- [ ] **Step 9: Hiển thị "đang gõ..." phía trên ô input**

Ngay TRƯỚC khối `{/* Input Message Area */}` trong JSX, thêm:
```tsx
        {/* Typing Indicator */}
        {typingUsers.length > 0 && (
          <div style={{
            padding: '4px 20px',
            fontSize: '11px',
            color: '#a3e635',
            fontFamily: 'monospace',
            fontStyle: 'italic'
          }}>
            {typingUsers.join(', ')} đang gõ...
          </div>
        )}
```

- [ ] **Step 10: Chạy dev server và xác minh thủ công**

Khởi động backend (`dotnet run --project backend/NexivraChatBackend`) và frontend (`npm run dev` trong thư mục frontend). Mở 2 trình duyệt/2 tài khoản, vào cùng 1 phòng. Xác minh:
- Header hiện `● 2 online: <tên1>, <tên2>`.
- Tab A gõ → tab B thấy `<tên A> đang gõ...`; A ngừng 2s hoặc gửi → biến mất.
- Đóng tab A → tab B thấy số online giảm trong vài giây.
- `@copilot ...` vẫn stream phản hồi bình thường (không lẫn vào typing người dùng).

- [ ] **Step 11: Commit**

```bash
git add frontend/nexivra-chat-frontend/src/views/ChatView.tsx
git commit -m "feat: show presence and typing indicator in ChatView"
```

---

### Task 5: Cập nhật context.md

**Files:**
- Modify: `context.md` (gốc repo)

- [ ] **Step 1: Cập nhật cấu trúc thư mục & schema/luồng**

Trong `context.md`:
- Thêm `Services/PresenceTracker.cs` vào sơ đồ cây thư mục backend với mô tả: "Theo dõi presence in-memory (ai online ở phòng nào), đếm theo connectionId để xử lý nhiều tab."
- Thêm dòng cho test project `backend/NexivraChatBackend.Tests/` — "Unit test xUnit cho PresenceTracker."
- Bổ sung mục sự kiện SignalR mới: `PresenceUpdate`, `TypingUpdate`, method `Typing`, `OnDisconnectedAsync`.
- Ghi chú frontend `ChatView.tsx` nay hiển thị online count + typing indicator.

- [ ] **Step 2: Cập nhật mục "Điểm Cần Khắc Phục"**

Trong `context.md`, mục Critical Bugs:
- Đánh dấu lỗi #1 (`LightBulbOutlined`) là ĐÃ SỬA (code thực tế đã dùng `BulbOutlined`).
- Cập nhật #3 (Gemini key): key đặt trong `appsettings.Development.json` (gitignored); fallback mock đã hoạt động sẵn.

- [ ] **Step 3: Commit**

```bash
git add context.md
git commit -m "docs: update context.md with presence/typing feature"
```

---

## Notes
- Không có dependency mới ở frontend (chỉ dùng `@microsoft/signalr`, antd sẵn có).
- Backend thêm 1 test project (xUnit) — không ảnh hưởng runtime web.
- Toàn bộ tính năng in-memory: restart server thì presence reset, đúng thiết kế MVP.
