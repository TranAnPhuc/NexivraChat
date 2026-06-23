# Design Spec — Foundation Fixes + Presence & Typing Indicator

**Ngày:** 2026-06-23
**Dự án:** NexivraChat — Real-time AI Chat Copilot
**Phạm vi đợt này:** (1) Sửa nền tảng cho chạy ổn end-to-end, (2) Thêm tính năng Presence (online users) + Typing indicator dạng in-memory.

---

## 1. Mục tiêu & Bối cảnh

Hệ thống chat realtime (.NET 8 + SignalR + Dapper + PostgreSQL, frontend React 19 + antd) đã có Auth, Rooms, ChatHub, AiService (Gemini streaming). Đợt này:

1. **Ổn định nền tảng:** sửa các lỗi đang treo để chạy được login → chat realtime → AI stream.
2. **Thêm tính năng:** hiển thị ai đang online trong phòng đang mở + chỉ báo "đang gõ...".

Ràng buộc nền tảng giữ nguyên: Dapper (không EF Core), antd, theme terminal tối, **không dùng màu tím**, màu nhấn `#a3e635`.

---

## 2. Mảng 1 — Sửa nền tảng (Foundation)

### 2.1. Lỗi import CopilotPanel
- File `frontend/nexivra-chat-frontend/src/components/CopilotPanel.tsx`: thay `<LightBulbOutlined />` → `<BulbOutlined />` (icon đã được import đúng tên `BulbOutlined`).

### 2.2. Cài đặt thư viện frontend
- Chạy `npm install` trong `frontend/nexivra-chat-frontend/` để có `node_modules` trước khi dev/build.

### 2.3. Cấu hình Gemini API Key (an toàn với Git)
- `AiService` **đã có sẵn** logic fallback: có key → gọi Gemini thật; không key → chạy mock streaming. Không cần đổi logic.
- Tạo `backend/NexivraChatBackend/appsettings.Development.json` chứa key thật:
  ```json
  { "Gemini": { "ApiKey": "<KEY_CỦA_USER>" } }
  ```
- `appsettings.json` giữ placeholder rỗng: `"Gemini": { "ApiKey": "" }`.
- Thêm `appsettings.Development.json` vào `.gitignore` để **không commit key** lên repo.

---

## 3. Mảng 2 — Presence + Typing Indicator (in-memory)

Toàn bộ trạng thái sống trong bộ nhớ server, **không đụng schema DB, không migration**. Mất khi restart server (chấp nhận được cho MVP). Phạm vi: **phòng đang mở**.

### 3.1. Backend — `PresenceTracker` (service singleton)

Nguồn sự thật duy nhất cho "ai đang ở phòng nào".

Cấu trúc dữ liệu:
```
roomId (int) → set { connectionId (string), username (string) }
```

- Một user mở nhiều tab = nhiều `connectionId`. Chỉ coi user là **offline khi tất cả** connection của user đó trong phòng đã rời đi (đếm theo connection để tránh nhấp nháy online/offline).
- Bảo vệ truy cập đa luồng bằng `lock` (hoặc `ConcurrentDictionary` + khóa cục bộ).

API nội bộ:
- `UserJoined(int roomId, string connId, string username)`
- `UserLeft(int roomId, string connId, string username)`
- `RemoveConnection(string connId)` → trả về danh sách `roomId` bị ảnh hưởng (dùng cho disconnect)
- `string[] GetOnlineUsers(int roomId)` → danh sách username distinct, đã online

Đăng ký DI: `services.AddSingleton<PresenceTracker>()` trong `Program.cs`.

### 3.2. Backend — Sự kiện SignalR mới trong `ChatHub`

| Hành động | Client gọi | Hub phát |
|---|---|---|
| Vào phòng | `JoinRoom(roomId)` (đã có, bổ sung gọi tracker) | `PresenceUpdate(roomId, string[] usernames)` tới `Clients.Group` |
| Rời phòng | `LeaveRoom(roomId)` (đã có, bổ sung gọi tracker) | `PresenceUpdate(roomId, usernames)` |
| Mất kết nối | `OnDisconnectedAsync` (THÊM MỚI) | `PresenceUpdate(roomId, usernames)` cho mọi phòng user đó đang ở |
| Gõ phím | `Typing(roomId, bool isTyping)` (THÊM MỚI) | `TypingUpdate(roomId, username, isTyping)` tới `Clients.OthersInGroup` |

- `ChatHub` hiện **chưa có** `OnDisconnectedAsync` → phải thêm để dọn presence khi user đóng tab / rớt mạng. Đây là điểm dễ quên nhất.
- `username` lấy từ `Context.User?.Identity?.Name` (JWT) — đã có sẵn.
- `Typing` phát cho **người khác** trong phòng (`OthersInGroup`), không gửi lại cho chính người gõ.

### 3.3. Frontend — Typing chống spam (trong `ChatView`)

- Khi gõ vào ô input:
  - Lần gõ đầu (đang `false` → có ký tự) → gọi `Typing(roomId, true)`.
  - Debounce **~2 giây**: ngừng gõ 2s → gọi `Typing(roomId, false)`.
  - Khi gửi tin nhắn → gọi `Typing(roomId, false)` ngay.
- Chỉ gửi khi **đổi trạng thái**, không gửi mỗi phím.

### 3.4. Frontend — Hiển thị (`ChatView`)

- **State mới:** `onlineUsers: string[]`, `typingUsers: string[]`.
- **Listener mới:** `PresenceUpdate(roomId, usernames)`, `TypingUpdate(roomId, username, isTyping)`. Nhớ `connection.off(...)` trong cleanup.
- **Presence UI:** tại Room Header (cạnh `#room_name`), thêm `● N online`, màu `#a3e635`, font monospace; liệt kê tên online (inline hoặc tooltip).
- **Typing UI:** dòng nhỏ ngay trên ô input: `user1 đang gõ...` / `user1, user2 đang gõ...`. Tự ẩn khi rỗng.
- AI Copilot vẫn dùng cơ chế placeholder/stream riêng đã có — **không** trộn vào typing của người dùng.

### 3.5. Chuyển phòng
- Khi đổi phòng: leave phòng cũ + join phòng mới (logic đã có trong `ChatView` effect theo `activeRoomId`). Khi rời phòng cũ phải reset `onlineUsers`/`typingUsers` của phòng cũ.

---

## 4. Files dự kiến chạm

**Backend:**
- `Services/PresenceTracker.cs` (mới)
- `Hubs/ChatHub.cs` (sửa `JoinRoom`/`LeaveRoom`, thêm `OnDisconnectedAsync`, `Typing`)
- `Program.cs` (đăng ký singleton)
- `appsettings.json` (placeholder key rỗng)
- `appsettings.Development.json` (mới, chứa key thật — gitignored)
- `.gitignore` (thêm `appsettings.Development.json`)

**Frontend:**
- `src/components/CopilotPanel.tsx` (sửa icon)
- `src/views/ChatView.tsx` (presence + typing state, listeners, UI, debounce)

---

## 5. Out of scope (YAGNI)
- Presence online toàn hệ thống (mọi phòng / sidebar).
- "Last seen" lưu DB.
- Persistence presence qua restart.
- Typing indicator cho tin nhắn riêng tư / DM (chưa có DM).

---

## 6. Tiêu chí thành công
- Login → chọn phòng → gửi tin nhắn realtime hoạt động; `@copilot` stream phản hồi (mock hoặc Gemini thật tùy key).
- Mở 2 tab cùng phòng (2 user khác nhau): mỗi bên thấy số online đúng và tên người kia.
- User A gõ → user B thấy "A đang gõ..."; A ngừng 2s hoặc gửi → chỉ báo biến mất.
- User đóng tab → bên còn lại thấy số online giảm trong vài giây.
- Key Gemini không xuất hiện trong `git status` / lịch sử commit.
