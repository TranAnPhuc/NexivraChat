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
- **AI Integration**: Gemini API (`gemini-2.5-flash`) truyền dữ liệu dạng streaming qua SignalR.

---

## 📁 Cấu Trúc Thư Mục & Các File Quan Trọng

```plaintext
NexivraChat/
├── backend/
│   └── NexivraChatBackend/
│       ├── Controllers/
│       │   ├── AuthController.cs          # Đăng ký & Đăng nhập, băm mật khẩu BCrypt, cấp JWT.
│       │   ├── RoomsController.cs         # Lấy danh sách phòng, lịch sử tin nhắn của phòng (phân trang).
│       │   └── ProfileController.cs       # API phân tích và quản lý Profile.
│       ├── Data/
│       │   ├── DapperContext.cs           # Khởi tạo kết nối NpgsqlConnection, cấu hình Map tên thuộc tính dạng snake_case.
│       │   └── DbInitializer.cs           # Tự động tạo bảng (users, chat_rooms, messages) và seed dữ liệu phòng mặc định.
│       ├── Hubs/
│       │   └── ChatHub.cs                 # Trọng tâm điều phối SignalR: gửi tin nhắn, tham gia phòng, điều phối stream AI, quản lý presence & typing.
│       ├── Models/
│       │   ├── User.cs                    # Bản ghi User (id, username, password_hash, created_at).
│       │   ├── ChatRoom.cs                # Bản ghi ChatRoom (id, name, description).
│       │   └── Message.cs                 # Bản ghi Message (id, room_id, sender_name, content, created_at, is_ai).
│       ├── Repositories/
│       │   ├── UserRepository.cs          # Truy vấn dữ liệu User bằng Dapper.
│       │   ├── RoomRepository.cs          # Truy vấn dữ liệu Room bằng Dapper.
│       │   ├── MessageRepository.cs       # Lưu tin nhắn mới và lấy lịch sử tin nhắn cũ.
│       │   └── ProfileRepository.cs       # CRUD thông tin Profile & dữ liệu AI phân tích.
│       ├── Services/
│       │   ├── TokenService.cs            # Tạo mã JWT Token thời hạn 7 ngày.
│       │   ├── AiService.cs               # Gọi trực tiếp REST API của Gemini với luồng stream IAsyncEnumerable<string>.
│       │   ├── PresenceTracker.cs         # Theo dõi presence in-memory (ai online ở phòng nào), đếm theo connectionId để xử lý nhiều tab.
│       │   └── TranslationService.cs      # Gọi Gemini API dịch thuật.
│       ├── Properties/
│       │   └── launchSettings.json        # Cấu hình cổng chạy backend (HTTP: 5182, HTTPS: 7103).
│       ├── Program.cs                     # Cấu hình ứng dụng: CORS, JWT Auth, SignalR mapping, DI Container.
│       └── appsettings.json               # Chuỗi kết nối DB PostgreSQL và khóa bí mật JWT.
│   └── NexivraChatBackend.Tests/
│       └── PresenceTrackerTests.cs        # Unit test xUnit cho PresenceTracker (6 test cases).
│
├── frontend/
│   └── nexivra-chat-frontend/
│       ├── src/
│       │   ├── components/
│       │   │   ├── CopilotPanel.tsx       # Bảng công cụ AI với giao diện PipelinePro (Tóm tắt phòng chat, Gợi ý chủ đề, Giải nghĩa thuật ngữ), nội dung Việt thân thiện.
│       │   │   ├── RoomSidebar.tsx        # Danh sách phòng chat, tạo phòng mới, thông tin user và nút đăng xuất, hỗ trợ light/dark, nội dung Việt.
│       │   │   └── ThemeToggle.tsx        # Nút bóng đèn ở header để chuyển đổi giữa light/dark theme.
│       │   ├── theme/
│       │   │   └── ThemeContext.tsx       # Context provider cho theme management, hook `useTheme`, hàm `getInitialTheme`, lưu preference vào localStorage.
│       │   ├── views/
│       │   │   ├── LoginView.tsx          # Màn hình đăng nhập/đăng ký thiết kế PipelinePro (teal primary, Inter/Outfit font), nội dung Việt.
│       │   │   ├── ChatView.tsx           # Giao diện chính kết nối SignalR Client, hiển thị tin nhắn, stream AI, online count, typing indicator, hỗ trợ light/dark, nội dung Việt.
│       │   │   └── ProfileView.tsx        # Màn hình xem profile cá nhân và phân tích tính cách AI.
│       │   ├── services/
│       │   │   └── api.ts                 # Cấu hình Axios, tự động đính kèm JWT Token vào Header của các yêu cầu API.
│       │   ├── App.tsx                    # Quản lý trạng thái đăng nhập, ThemeProvider + antd ConfigProvider (colorPrimary teal + light/dark algorithm).
│       │   ├── index.css                  # CSS variables (token) cho design system PipelinePro, color, typography, `:root[data-theme="light"|"dark"]`.
│       │   └── main.tsx                   # Điểm khởi đầu React, set `data-theme` attribute trước render, nạp Google Fonts (Inter/Outfit).
│       ├── index.html                     # Nạp Google Fonts (Inter/Outfit), meta viewport, root div.
│       ├── package.json                   # Các gói phụ thuộc (antd, @microsoft/signalr, axios, vite).
│       └── vite.config.ts                 # Cấu hình Vite.
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
    
    Hub->>Hub: Tạo tempAiMessageId (số âm ngẫu nhiên)
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
Luồng xử lý tự động phân tích tính cách và hành vi người dùng qua AI:
- **Thu thập dữ liệu**: Hệ thống lấy lịch sử trò chuyện (tối đa 30 tin nhắn gần nhất) của người dùng từ cơ sở dữ liệu PostgreSQL.
- **Phân tích hành vi**: Gửi prompt được thiết kế sẵn (bao gồm lịch sử chat) đến Gemini API để đánh giá tính cách, sở thích, và xu hướng giao tiếp.
- **Lưu trữ dữ liệu**: Nhận kết quả phân tích dưới dạng cấu trúc JSON từ Gemini và lưu vào trường `ai_analysis_json` (kiểu dữ liệu `JSONB`) trong bảng `user_profiles` cùng với thời gian phân tích `last_analyzed_at`.
- **Hiển thị (Frontend)**: Màn hình `ProfileView.tsx` gọi API lấy thông tin Profile và render giao diện trực quan hóa các chỉ số thông minh, tính cách dưới dạng biểu đồ/thẻ chỉ số thông minh hiện đại (PipelinePro UI).

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

