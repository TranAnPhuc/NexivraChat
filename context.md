# 🌐 Real-time AI Chat Copilot - Project Context

Tài liệu này cung cấp cái nhìn toàn diện về cấu trúc thư mục, kiến trúc kỹ thuật, luồng dữ liệu và trạng thái hiện tại của dự án **AI Chat Realtime MVP**. Tài liệu giúp các phiên làm việc tiếp theo hiểu ngay hệ thống mà không cần đọc lại toàn bộ mã nguồn.

---

## 🛠️ Công Nghệ Sử Dụng (Tech Stack)

Hệ thống được xây dựng trên mô hình Client-Server tách biệt:

- **Backend**: .NET 8 Web API, SignalR Hub quản lý kết nối thời gian thực.
- **Database**: PostgreSQL kết hợp **Dapper** (truy vấn SQL thuần, tuyệt đối không dùng EF Core).
- **Frontend**: React 19, TypeScript, Ant Design (antd) làm UI system.
  - **Giao diện**: Thiết kế kiểu PipelinePro với nền sáng mặc định + chế độ tối (light/dark), màu chính teal `#0D9488`.
  - **Font chữ**: Inter cho nội dung, Outfit cho tiêu đề (tải từ Google Fonts).
  - **Styling**: Bo góc mềm, CSS variables (token) ở `src/index.css` với `:root[data-theme="light"|"dark"]` cho theme consistency.
  - **Theme Control**: `ThemeContext` + nút `ThemeToggle`, lưu tùy chọn vào `localStorage` (key `nexivra-theme`).
  - **antd Integration**: `ConfigProvider` đồng bộ màu component (colorPrimary teal + light/dark algorithm).
  - **Ghi chú**: Tuân thủ không dùng màu tím (Purple Ban) — màu chính là teal, không phải indigo.
- **AI Integration**: Gemini API (`gemini-2.5-flash`) truyền dữ liệu dạng streaming qua SignalR. `AiService`/`TranslationService` dùng `HttpClient` được quản lý bởi `IHttpClientFactory`.
- **Hiệu năng (Backend)**: Toàn bộ truy cập DB qua Dapper là **async all-the-way** (không block thread pool); có index trên bảng `messages`; HttpClient được pool qua `IHttpClientFactory`. Xem mục "Lịch Sử Tối Ưu Hiệu Năng" bên dưới.

---

## 📁 Cấu Trúc Thư Mục & Các File Quan Trọng

```plaintext
NexivraChat/
├── backend/
│   └── NexivraChatBackend/
│       ├── Controllers/
│       │   ├── AuthController.cs          # Đăng ký & Đăng nhập (async), băm mật khẩu PasswordHasher, cấp JWT.
│       │   ├── RoomsController.cs         # Lấy danh sách phòng, lịch sử tin nhắn của phòng (phân trang) — async.
│       │   ├── UsersController.cs         # Danh sách user, tạo/lấy hội thoại 1-1, lịch sử tin nhắn riêng tư — async.
│       │   ├── ProfileController.cs       # API xem/cập nhật Profile & phân tích tính cách AI — async.
│       │   └── TranslationController.cs   # Endpoint POST /api/translation dịch tin nhắn qua TranslationService.
│       ├── Data/
│       │   ├── DapperContext.cs           # Khởi tạo kết nối NpgsqlConnection, cấu hình Map tên thuộc tính dạng snake_case.
│       │   └── DbInitializer.cs           # Tự tạo bảng (users, chat_rooms, private_chats, messages, user_profiles), tạo INDEX cho messages, seed phòng mặc định. (Đồng bộ — chạy 1 lần lúc khởi động.)
│       ├── Hubs/
│       │   └── ChatHub.cs                 # Trọng tâm điều phối SignalR: gửi tin nhắn (nhóm & 1-1), điều phối stream AI, presence & typing. Mọi truy cập DB đã async.
│       ├── Models/
│       │   ├── User.cs                    # Bản ghi User (id, username, password_hash, created_at).
│       │   ├── ChatRoom.cs                # Bản ghi ChatRoom (id, name, description).
│       │   ├── Message.cs                 # Bản ghi Message (id, room_id, private_chat_id, sender_name, content, created_at, is_ai).
│       │   ├── PrivateChat.cs             # Bản ghi PrivateChat (id, user1_id, user2_id, created_at).
│       │   └── UserProfile.cs             # Bản ghi UserProfile (user_id, bio, native_language, ai_analysis_json, last_analyzed_at).
│       ├── Repositories/                  # Tất cả repository dùng Dapper ASYNC (QueryAsync/ExecuteScalarAsync/...), cột tường minh (không SELECT *).
│       │   ├── UserRepository.cs          # Truy vấn dữ liệu User.
│       │   ├── RoomRepository.cs          # Truy vấn dữ liệu Room.
│       │   ├── MessageRepository.cs       # Lưu tin nhắn mới & lấy lịch sử (theo phòng / chat 1-1 / người gửi).
│       │   ├── PrivateChatRepository.cs   # Lấy hoặc tạo hội thoại 1-1 (đảm bảo u1<u2 cho UNIQUE).
│       │   └── ProfileRepository.cs       # CRUD Profile & dữ liệu AI phân tích (upsert ::jsonb).
│       ├── Services/
│       │   ├── TokenService.cs            # Tạo mã JWT Token thời hạn 7 ngày.
│       │   ├── AiService.cs               # Gọi Gemini REST (stream IAsyncEnumerable<string>) — HttpClient inject qua IHttpClientFactory.
│       │   ├── TranslationService.cs      # Gọi Gemini dịch thuật — HttpClient inject qua IHttpClientFactory.
│       │   ├── PresenceTracker.cs         # Theo dõi presence in-memory, đếm theo connectionId để xử lý nhiều tab.
│       │   └── TempMessageId.cs           # Sinh temp-ID âm DUY NHẤT cho tin nhắn AI đang stream (Interlocked.Decrement).
│       ├── Properties/
│       │   └── launchSettings.json        # Cấu hình cổng chạy backend (HTTP: 5182, HTTPS: 7103).
│       ├── Program.cs                     # Cấu hình ứng dụng: CORS, JWT Auth, SignalR, DI Container, AddHttpClient<AiService/TranslationService>.
│       └── appsettings.json               # Chuỗi kết nối DB PostgreSQL và khóa bí mật JWT.
│   └── NexivraChatBackend.Tests/
│       ├── Fixtures/
│       │   ├── DatabaseFixture.cs         # Quản lý vòng đời Docker container chạy Postgres tự động (Testcontainers) và Respawn dọn dẹp dữ liệu.
│       │   └── DatabaseCollection.cs      # Định nghĩa xUnit Collection để chia sẻ DatabaseFixture.
│       ├── Integration/
│       │   ├── ChatHubTests.cs            # Integration test kiểm thử SignalR ChatHub & phân quyền tham gia DM.
│       │   └── ConversationReadRepositoryTests.cs # Integration test cho repository với logic unread count và UPSERT.
│       ├── PresenceTrackerTests.cs        # Unit test xUnit cho PresenceTracker (6 test cases).
│       └── TempMessageIdTests.cs          # Unit test xUnit cho TempMessageId (2 test: âm & duy nhất khi gọi đồng thời). Tổng cộng 13 test.
│
├── frontend/
│   └── nexivra-chat-frontend/
│       ├── src/
│       │   ├── components/
│       │   │   ├── CopilotPanel.tsx       # Bảng công cụ AI với giao diện PipelinePro (Tóm tắt phòng chat, Gợi ý chủ đề, Giải nghĩa thuật ngữ), nội dung Việt thân thiện.
│       │   │   ├── RoomSidebar.tsx        # Danh sách phòng chat + danh sách user (chat 1-1), tạo phòng mới, thông tin user và nút đăng xuất, hỗ trợ light/dark, nội dung Việt.
│       │   │   ├── MessageBubble.tsx      # Bong bóng 1 tin nhắn, bọc React.memo — khi AI stream chỉ bong bóng đang stream re-render. Gồm tên (mở hồ sơ), nội dung, nút Dịch & bản dịch.
│       │   │   ├── ThemeToggle.tsx        # Nút bóng đèn ở header để chuyển đổi giữa light/dark theme.
│       │   │   └── Logo.tsx               # Logo thương hiệu Nexivra.
│       │   ├── theme/
│       │   │   └── ThemeContext.tsx       # Context provider cho theme management, hook `useTheme`, hàm `getInitialTheme`, lưu preference vào localStorage.
│       │   ├── views/
│       │   │   ├── LoginView.tsx          # Màn hình đăng nhập/đăng ký thiết kế PipelinePro (teal primary, Inter/Outfit font), nội dung Việt.
│       │   │   ├── ChatView.tsx           # Giao diện chính kết nối SignalR Client, hiển thị tin nhắn (nhóm & 1-1), stream AI, dịch tin, online count, typing indicator, hỗ trợ light/dark.
│       │   │   └── ProfileView.tsx        # Màn hình xem profile cá nhân và phân tích tính cách AI (radar/thẻ chỉ số).
│       │   ├── services/
│       │   │   └── api.ts                 # Cấu hình Axios, tự động đính kèm JWT Token vào Header của các yêu cầu API.
│       │   ├── App.tsx                    # Quản lý trạng thái đăng nhập, ThemeProvider + antd ConfigProvider (colorPrimary teal + light/dark algorithm).
│       │   ├── index.css                  # CSS variables (token) cho design system PipelinePro, color, typography, `:root[data-theme="light"|"dark"]`.
│       │   └── main.tsx                   # Điểm khởi đầu React, set `data-theme` attribute trước render, nạp Google Fonts (Inter/Outfit).
│       ├── index.html                     # Nạp Google Fonts (Inter/Outfit), meta viewport, root div.
│       ├── package.json                   # Các gói phụ thuộc (antd, @microsoft/signalr, axios, vite).
│       └── vite.config.ts                 # Cấu hình Vite.
│
└── docs/
    └── superpowers/
        ├── specs/                         # Thiết kế (spec) các đợt tối ưu hiệu năng theo giai đoạn.
        └── plans/                         # Plan thực thi chi tiết từng giai đoạn (GĐ1, GĐ2, GĐ3).
```

---

## 🗄️ Database Schema (PostgreSQL)

```sql
-- 1. Bảng Users (Lưu thông tin tài khoản)
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(50) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- 2. Bảng ChatRooms (Các phòng chat nhóm)
CREATE TABLE chat_rooms (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description VARCHAR(255),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- 3. Bảng Messages (Lưu lịch sử tin nhắn bao gồm tin nhắn của AI và tin nhắn 1-1)
CREATE TABLE messages (
    id SERIAL PRIMARY KEY,
    room_id INT REFERENCES chat_rooms(id) ON DELETE CASCADE, -- Nullable đối với chat 1-1
    private_chat_id INT REFERENCES private_chats(id) ON DELETE CASCADE, -- Nullable đối với chat nhóm
    sender_id INT NULL REFERENCES users(id) ON DELETE SET NULL, -- ID người gửi (NULL cho AI)
    sender_name VARCHAR(50) NOT NULL,
    content TEXT NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
    is_ai BOOLEAN DEFAULT FALSE NOT NULL
);

-- 4. Bảng PrivateChats (Hội thoại riêng tư 1-1)
CREATE TABLE private_chats (
    id SERIAL PRIMARY KEY,
    user1_id INT REFERENCES users(id) ON DELETE CASCADE,
    user2_id INT REFERENCES users(id) ON DELETE CASCADE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
    CONSTRAINT unique_users UNIQUE (user1_id, user2_id)
);

-- 5. Bảng UserProfiles (Hồ sơ người dùng và phân tích AI)
CREATE TABLE user_profiles (
    user_id INT PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    bio VARCHAR(255),
    native_language VARCHAR(50) DEFAULT 'Vietnamese' NOT NULL,
    ai_analysis_json JSONB, -- Lưu kết quả đánh giá tính cách dạng JSON bằng AI
    last_analyzed_at TIMESTAMP
);

-- 6. Index tối ưu truy vấn lịch sử tin nhắn (DbInitializer tạo, CREATE INDEX IF NOT EXISTS)
CREATE INDEX idx_messages_room_created    ON messages (room_id, created_at);
CREATE INDEX idx_messages_private_created ON messages (private_chat_id, created_at);
CREATE INDEX idx_messages_sender          ON messages (sender_name);
CREATE INDEX idx_messages_sender_id       ON messages (sender_id);
```

---

## 🔄 Quy Trình Xử Lý Real-time & Stream AI (SignalR)

```mermaid
sequenceDiagram
    participant Client as Frontend (Vite + React)
    participant Hub as SignalR (ChatHub)
    participant Repo as Database (Dapper)
    participant AI as Gemini API (AiService)

    Client->>Hub: SendMessage(roomId, "@copilot Làm sao để học .NET?")
    activate Hub

    Hub->>Repo: SaveNewMessage(User Message)
    Hub->>Client: ReceiveMessage(User Message)

    Note over Hub: Phát hiện tiền tố @copilot. Gọi Gemini API.

    Hub->>Hub: Tạo tempAiMessageId (số âm tuần tự, duy nhất qua TempMessageId.Next)
    Hub->>Client: ReceiveMessage(AI Placeholder với ID âm)

    loop Stream token từ Gemini
        AI-->>Hub: Gửi token mới
        Hub->>Client: ReceiveAiToken(tempAiMessageId, token)
        Note over Client: Cập nhật ký tự nhận được vào Bubble chat tức thì
    end

    Hub->>Repo: SaveNewMessage(Tin nhắn AI hoàn chỉnh)
    Hub->>Client: ReceiveAiComplete(tempAiMessageId, finalAiMessageId, finalContent)
    Note over Client: Thay thế ID âm bằng ID thật từ Database
    deactivate Hub
```

### Sự Kiện SignalR Mới: Presence & Typing (từ phòng ban)

**Hub Methods** (gọi từ Client):

- `JoinRoom(roomId)` — Cập nhật: người dùng tham gia phòng, phát `PresenceUpdate` cho toàn phòng với danh sách online hiện tại.
- `LeaveRoom(roomId)` — Cập nhật: người dùng rời phòng, phát `PresenceUpdate` và reset `TypingUpdate` cho người khác.
- `Typing(roomId, isTyping)` — Client phát: khi bắt đầu hoặc dừng gõ (debounced 2s), phát `TypingUpdate` cho người khác.
- `OnDisconnectedAsync()` — Tự động: khi ngắt kết nối, dọn sạch presence khỏi tất cả phòng và phát lại `PresenceUpdate`.

**Client Events** (nhận từ Hub):

- `PresenceUpdate(roomId, usernames[])` — Danh sách tên người dùng online hiện tại, hiển thị online count ở phòng header.
- `TypingUpdate(roomId, username, isTyping)` — Ai đang gõ, hiển thị thông báo "username đang gõ..." trên giao diện.

**Tracking**: `PresenceTracker` singleton lưu `(roomId -> (connectionId -> username))`, hỗ trợ một user mở nhiều tab (chỉ offline khi tất cả connection rời).

---

## 🤖 Kiến Trúc Tính Năng AI Mới

### 1. Tính năng Dịch tin nhắn Real-time

Luồng xử lý khi người dùng chọn dịch tin nhắn:

- **Client (Frontend)**: Người dùng nhấn vào nút Dịch trên bong bóng chat của một tin nhắn cụ thể.
- **REST API (Backend)**: Gửi yêu cầu dịch đến `ProfileController` hoặc một endpoint dịch thuật.
- **TranslationService**: Nhận tin nhắn gốc và ngôn ngữ đích (lấy từ tùy chọn của Profile người dùng, mặc định là Vietnamese).
- **Gemini API**: `TranslationService` gọi Gemini API bằng prompt dịch thuật chuyên biệt để dịch nội dung tin nhắn một cách tự nhiên nhất.
- **Phản hồi & Render**: Kết quả dịch được trả về cho Client qua REST API và hiển thị ngay dưới dạng bong bóng chat phụ bên dưới tin nhắn gốc.

### 2. Tính năng Phân tích hồ sơ AI Profile Analyzer

\Luồng xử lý tự động phân tích tính cách và hành vi người dùng qua AI:

- **Thu thập dữ liệu**: Hệ thống lấy lịch sử trò chuyện (tối đa 30 tin nhắn gần nhất) của người dùng từ cơ sở dữ liệu PostgreSQL.
- **Phân tích hành vi**: Gửi prompt được thiết kế sẵn (bao gồm lịch sử chat) đến Gemini API để đánh giá tính cách, sở thích, và xu hướng giao tiếp.
- **Lưu trữ dữ liệu**: Nhận kết quả phân tích dưới dạng cấu trúc JSON từ Gemini và lưu vào trường `ai_analysis_json` (kiểu dữ liệu `JSONB`) trong bảng `user_profiles` cùng với thời gian phân tích `last_analyzed_at`.
- **Hiển thị (Frontend)**: Màn hình `ProfileView.tsx` gọi API lấy thông tin Profile và render giao diện trực quan hóa các chỉ số thông minh, tính cách dưới dạng biểu đồ/thẻ chỉ số thông minh hiện đại (PipelinePro UI).

---

## 🧪 Hạ Tầng Kiểm Thử Tích Hợp (Integration Test Harness)

Để đảm bảo tính đúng đắn và độc lập của các logic nghiệp vụ phức tạp (như tính toán số tin nhắn chưa đọc unread badges và phân quyền SignalR ChatHub), dự án thiết lập hệ thống kiểm thử tích hợp tự động (Integration Test Suite):

- **Testcontainers.PostgreSql**: Tự động kéo và chạy một Docker container PostgreSQL (`postgres:15-alpine`) khi khởi chạy test suite. Cấu trúc DB được khởi tạo tự động thông qua `DbInitializer`.
- **Respawn**: Reset trạng thái của tất cả các bảng dữ liệu về trạng thái sạch sẽ trước/sau mỗi test case để tránh ảnh hưởng lẫn nhau giữa các test case, giúp tăng tốc độ kiểm thử mà không cần dựng lại database từ đầu.
- **WebApplicationFactory (SignalR Integration)**: Tích hợp SignalR client thực tế kết nối tới server in-memory qua `WebApplicationFactory<Program>`, sử dụng token JWT giả lập để kiểm thử phân quyền truy cập hội thoại riêng tư trong `ChatHub.MarkRead`.

---

## 🚀 Lịch Sử Tối Ưu Hiệu Năng (theo giai đoạn)

Tài liệu thiết kế & plan chi tiết nằm trong `docs/superpowers/`. Thực thi theo quy trình subagent-driven (mỗi task có review spec + chất lượng, cuối mỗi giai đoạn có whole-branch review).

### Giai đoạn 1 — Quick Wins ✅ HOÀN TẤT

- **Index DB**: thêm `idx_messages_room_created`, `idx_messages_private_created`, `idx_messages_sender` trong `DbInitializer` (idempotent).
- **Bỏ `SELECT *`**: mọi repository dùng danh sách cột tường minh.
- **Temp-ID AI**: thay `new Random()` bằng `TempMessageId.Next()` (`Interlocked.Decrement`) — temp-ID âm tuần tự, duy nhất, có unit test.
- **Auto-scroll**: chỉ smooth-scroll khi có tin nhắn mới; dùng `auto` khi AI đang stream token (hết giật).

### Giai đoạn 2 — Async Backend ✅ HOÀN TẤT

- **Async Dapper all-the-way**: tất cả 5 repository (Message/User/Room/PrivateChat/Profile) chuyển sang `QueryAsync`/`ExecuteScalarAsync`/`ExecuteAsync`; mọi caller (ChatHub + Controllers) `await` — không còn block thread pool.
- **IHttpClientFactory**: `AiService` + `TranslationService` nhận `HttpClient` qua DI (`AddHttpClient<T>()`) — hết nguy cơ socket exhaustion.
- Không còn sync-over-async (`.Result`/`.Wait()`) trong codebase.

### Giai đoạn 3 — Frontend Render ✅ HOÀN TẤT

- **Tách `MessageBubble` + `React.memo`**: trích JSX bong bóng khỏi `ChatView`; các handler bọc `useCallback` và dùng `usersRef` để callback ổn định → khi AI stream chỉ bong bóng đang stream re-render thay vì cả danh sách.
- **Lazy-load `ProfileView`** (`React.lazy` + `Suspense`, gate bằng `profileEverOpened`): tách chunk antd nặng (~116 kB) khỏi bundle ban đầu, chỉ tải sau lần đầu mở hồ sơ; giữ animation Modal.
- **Bỏ virtualize** (quyết định YAGNI: chỉ tải 50 tin/phòng nên lợi ích nhỏ, rủi ro với auto-scroll/stream cao).

### Giai đoạn 4 — Keyset Pagination (GĐ4.2), Resync Reconnect (GĐ4.3) & Migration sender_id (GĐ4.4) ✅ HOÀN TẤT

- **Phân trang Keyset theo ID (GĐ4.2)**: Chuyển đổi từ offset-based sang keyset-based (`beforeId`) tránh lệch tin hoặc trùng lặp khi có tin nhắn mới liên tục. Bỏ qua các ID tin nhắn AI tạm thời (giá trị âm) khi xác định ID tin nhắn cũ nhất.
- **Giữ vị trí cuộn (Scroll Preservation) (GĐ4.2)**: Sử dụng `flushSync` từ `react-dom` để ép React cập nhật DOM đồng bộ trước khi đo và bù trừ `scrollTop` của container, tránh hiện tượng giật màn hình khi tải tin cũ.
- **Tự động tải tin cũ (Intersection Observer) (GĐ4.2)**: Sử dụng API `IntersectionObserver` lắng nghe Sentinel Div ở đầu danh sách tin nhắn để tự động kích hoạt tải thêm lịch sử, đi kèm nút bấm liên kết thủ công làm fallback và hiển thị thông báo "Đầu hội thoại" khi đã tải hết.
- **Tự động Resync tin nhắn sau Reconnect (GĐ4.3)**:
  - **Backend**: Nâng cấp `MessageRepository` hỗ trợ tham số `afterId` (WHERE id > @afterId ORDER BY id ASC LIMIT @limit) cho cả Room và Private Chat, và expose qua API Query Param `?afterId=` tại 2 endpoints messages.
  - **Frontend**: Track ID tin nhắn thật lớn nhất của hội thoại đang mở (`lastReceivedMessageId`). Khi reconnect (SignalR `onreconnected`), tự động gọi API fetch tin nhắn nhỡ từ `afterId=lastReceivedMessageId` cho hội thoại đang active, gộp tin nhắn và lọc trùng lặp (deduplicate) bằng `id` âm thầm trước khi cập nhật state.
- **Migration sender_id (GĐ4.4)**:
  - **Database**: Bổ sung cột `sender_id INT NULL REFERENCES users(id) ON DELETE SET NULL` vào bảng `messages` trong `DbInitializer` (chạy migration idempotent + backfill dữ liệu cũ dựa trên `users.username` cho các tin không phải của AI), đồng thời bổ sung `idx_messages_sender_id`.
  - **Backend & Frontend**: Cập nhật `Message` model (thêm `SenderId`/`senderId`), `MessageRepository` (truy vấn Dapper tường minh có `sender_id`), `ChatHub` (trích xuất user ID từ JWT claim truyền vào `userMessage.SenderId`), và frontend TypeScript interface.

> **Ghi chú kỹ thuật còn lại (non-blocking):** `MessageRepository.GetOldMessages` là dead code (không caller) — có thể xóa sau; `TempMessageId` dùng `int`, lý thuyết underflow sau ~2 tỷ lần gọi (không đáng kể).

---

## 🎯 Quyết Định Chiến Lược & Hướng Đi Tiếp Theo

> **Quyết định (qua `/plan-ceo-review`, ngày 2026-06-27): Chọn HƯỚNG 2 — Hoàn thiện các chức năng nền tảng cần thiết nhất, TRƯỚC khi làm tính năng đột phá.**
>
> **Cập nhật (2026-06-28, `/plan-ceo-review`):** Mô hình làm việc = **Claude lập kế hoạch + review, Antigravity 2.0 viết code**. Roadmap thực thi chi tiết (GĐ4.x nền tảng → GĐ5 tính năng giống Zalo/Messenger/Telegram): `docs/superpowers/plans/roadmap-foundation.md`.

**Lý do (CEO review):** Lời hứa cốt lõi của một app chat là "tin nhắn đến nơi và người dùng không bỏ lỡ phản hồi". Phần AI (copilot streaming, dịch, profile analyzer, presence/typing) đã mạnh bất ngờ so với MVP — đó là điểm mạnh. Nhưng nền tảng chat còn vỡ ở những chỗ phá vỡ niềm tin. Làm nền tảng đáng tin trước thì tính năng đột phá (vd: chat đa ngôn ngữ real-time) sau này mới đứng trên nền vững.

**Khoảng trống nền tảng đã xác minh (cần làm — ưu tiên giảm dần):**

1. **Badge "chưa đọc" (unread) theo từng phòng/DM** — ✅ ĐÃ TRIỂN KHAI (branch `feat/unread-badges`). Xem mục "Tính năng Unread Badges" bên dưới.
2. **Tải thêm tin cũ (phân trang lịch sử)** — client luôn gọi `limit=50&offset=0` (`ChatView.tsx:122,132`), không có "tải tin cũ hơn" → lịch sử quá 50 tin không với tới được trong UI.
3. **Resync tin nhắn bị lỡ sau reconnect** — đã có `.withAutomaticReconnect()` (`ChatView.tsx:188`) nhưng khi kết nối lại KHÔNG fetch các tin nhắn phát sinh trong lúc mất mạng → mất tin trên giao diện.
4. **Trạng thái tin nhắn (đã gửi / đã nhận / đã xem)** — chưa có cột/sự kiện delivered/seen; người gửi không biết tin đã tới chưa.

**Cần kiểm tra thêm khi triển khai (nghi ngờ, chưa xác nhận):** phân quyền API lịch sử chat 1-1 (`UsersController` / `private-chat/{id}/messages`) — đảm bảo user A không đọc được hội thoại riêng của người khác qua API.

**Hoãn (để sau khi nền tảng vững):** Tính năng đột phá "Chat đa ngôn ngữ real-time" (tự dịch mọi tin đến sang tiếng mẹ đẻ của từng người đọc, tái dùng `TranslationService` + `native_language`). Đây là điểm khác biệt rẻ vì hạ tầng dịch đã có — ứng viên số 1 cho giai đoạn đột phá kế tiếp.

### Tính năng Unread Badges ✅ (branch `feat/unread-badges`, plan: `docs/superpowers/plans/unread-badges-plan.md`)
Server-authoritative: theo dõi "đã đọc đến đâu" cho mỗi user × hội thoại.
- **DB**: bảng `conversation_reads` (user_id, room_id|private_chat_id, last_read_message_id) + CHECK đúng-1-target + 2 partial unique index (`uq_reads_room`, `uq_reads_dm`); thêm index `(room_id, id)` / `(private_chat_id, id)` phục vụ truy vấn `id > last_read`.
- **Backend**: `ConversationReadRepository` (GetUnreadCounts, MarkRoomRead/MarkPrivateChatRead — upsert `ON CONFLICT` + `GREATEST` không lùi mốc); model `UnreadCounts`. Endpoint `GET /api/users/unread-counts`. Hub `MarkRead(roomId?, privateChatId?, lastReadMessageId)` có verify participant cho DM.
- **SignalR mới**: `UnreadUpdate {type, id}` (phòng phát `Clients.All` vì group không tới user ở phòng khác; DM phát tới người nhận, `id` = người gửi); `ReadUpdate {roomId, privateChatUserId}` đồng bộ đa tab.
- **Quy ước key**: phòng key theo `roomId`; DM key theo **id người đối thoại** (để sidebar liệt kê theo user tra cứu badge trực tiếp), trong khi storage/MarkRead vẫn theo `private_chat_id`.
- **Cold-start**: phòng CHƯA mở = 0 unread (INNER JOIN); DM chưa mở vẫn tính đủ (LEFT JOIN + COALESCE) vì DM mới là thứ không được bỏ lỡ.
- **Frontend**: badge ở `RoomSidebar` (DM = số teal `#0F766E`, phòng = dot + chữ đậm), `document.title = "(N) Nexivra Chat"`, mark-read khi mở/nhận tin ở hội thoại active, `onreconnected` refetch counts + tin hội thoại active (fold resync tối thiểu).
- **Còn lại (TODOS.md)**: Hạ tầng test tích hợp (Testcontainers/Respawn kết hợp cơ chế fallback PostgreSQL tự động và kiểm thử SignalR ChatHub) đã được xây dựng hoàn chỉnh và chạy thành công; gating focus/scroll cho mark-read là polish.

---

## ⚠️ Điểm Cần Khắc Phục Hiện Tại (Critical Bugs)

1. **Lỗi import ở `CopilotPanel.tsx`** ✅ ĐÃ SỬA:
   - Dòng 94 đã sử dụng `<BulbOutlined />` (không phải `<LightBulbOutlined />`). Sửa xong, không gây lỗi.
2. **Cài đặt thư viện Frontend** ✅ ĐÃ XONG:
   - Đã chạy `npm install`, thư mục `node_modules` đã có. (Nếu clone mới về thì vẫn cần chạy lại `npm install`.)
3. **Cấu hình Gemini API Key** ✅ ĐÃ SỬA:
   - API key đặt trong `appsettings.Development.json` (gitignored, không commit). `appsettings.json` giữ placeholder trống. `AiService` đã hỗ trợ fallback mock mode khi không có key.
4. **Lỗi PostgresException khi tạo tài khoản (Thiếu cột password_hash và lệch schema cũ)** ✅ ĐÃ SỬA:
   - Phát hiện DB PostgreSQL local tồn tại các bảng `users` và `messages` cũ bị lệch định dạng tên cột và thiếu các cột quan trọng (`password_hash`, `is_ai`).
   - Đã được khắc phục bằng cách DROP các bảng cũ để `DbInitializer` tự động tạo mới hoàn toàn cấu trúc bảng chuẩn khi khởi chạy ứng dụng.
