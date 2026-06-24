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
│       │   └── RoomsController.cs         # Lấy danh sách phòng, lịch sử tin nhắn của phòng (phân trang).
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
│       │   └── MessageRepository.cs       # Lưu tin nhắn mới và lấy lịch sử tin nhắn cũ.
│       ├── Services/
│       │   ├── TokenService.cs            # Tạo mã JWT Token thời hạn 7 ngày.
│       │   ├── AiService.cs               # Gọi trực tiếp REST API của Gemini với luồng stream IAsyncEnumerable<string>.
│       │   └── PresenceTracker.cs         # Theo dõi presence in-memory (ai online ở phòng nào), đếm theo connectionId để xử lý nhiều tab.
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
│       │   │   └── ChatView.tsx           # Giao diện chính kết nối SignalR Client, hiển thị tin nhắn, stream AI, online count, typing indicator, hỗ trợ light/dark, nội dung Việt.
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

-- 3. Bảng Messages (Lưu lịch sử tin nhắn bao gồm tin nhắn của AI)
CREATE TABLE messages (
    id SERIAL PRIMARY KEY,
    room_id INT REFERENCES chat_rooms(id) ON DELETE CASCADE,
    sender_name VARCHAR(50) NOT NULL,
    content TEXT NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
    is_ai BOOLEAN DEFAULT FALSE NOT NULL
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

## ⚠️ Điểm Cần Khắc Phục Hiện Tại (Critical Bugs)

1. **Lỗi import ở `CopilotPanel.tsx`** ✅ ĐÃ SỬA:
   - Dòng 94 đã sử dụng `<BulbOutlined />` (không phải `<LightBulbOutlined />`). Sửa xong, không gây lỗi.
2. **Cài đặt thư viện Frontend** ✅ ĐÃ XONG:
   - Đã chạy `npm install`, thư mục `node_modules` đã có. (Nếu clone mới về thì vẫn cần chạy lại `npm install`.)
3. **Cấu hình Gemini API Key** ✅ ĐÃ SỬA:
   - API key đặt trong `appsettings.Development.json` (gitignored, không commit). `appsettings.json` giữ placeholder trống. `AiService` đã hỗ trợ fallback mock mode khi không có key.
