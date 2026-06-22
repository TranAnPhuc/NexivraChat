# AI Chat Copilot - Collaborative Chat with AI Assistant

Hệ thống phòng chat thời gian thực hỗ trợ nhiều người dùng giao tiếp, tích hợp trợ lý AI Co-pilot (Assistant) tự động phân tích và phản hồi theo thời gian thực (stream từng chữ) qua SignalR Hub.

## 📌 Project Overview
- **Dự án:** AI Chat Realtime MVP (Option C)
- **Kiểu dự án:** WEB (ASP.NET Core Web API + React TS)
- **Tính năng chính:**
  1. Đăng ký & Đăng nhập (JWT Auth), mã hóa mật khẩu.
  2. Tạo phòng chat (Public/Private), lưu lịch sử chat.
  3. Giao tiếp thời gian thực giữa các thành viên qua SignalR.
  4. Trợ lý AI Co-pilot hoạt động trong phòng (tự động gợi ý phản hồi, giải thích từ ngữ hoặc tóm tắt hội thoại khi người dùng yêu cầu bằng cú pháp `@copilot`).
  5. Luồng phản hồi của AI được stream token-by-token qua SignalR Hub.

---

## 🏆 Success Criteria
- [ ] Người dùng đăng ký/đăng nhập thành công, nhận JWT Token.
- [ ] Tham gia vào phòng chat, gửi/nhận tin nhắn realtime với người dùng khác.
- [ ] Khi gửi tin nhắn bắt đầu bằng `@copilot`, AI nhận thức được ngữ cảnh hội thoại trước đó và stream câu trả lời từng từ về phòng chat qua SignalR.
- [ ] Lịch sử tin nhắn được lưu trữ đầy đủ trong PostgreSQL thông qua Dapper (không sử dụng Entity Framework).
- [ ] UI React mượt mà, hỗ trợ giao diện tối (Dark Mode), không dùng màu tím (tuân thủ Purple Ban).

---

## 🛠️ Tech Stack & Database Schema

### Tech Stack
- **Backend:** .NET 8 (ASP.NET Core Web API, SignalR Hub)
- **Database:** PostgreSQL + Dapper
- **Frontend:** React 19 + TypeScript + Ant Design (antd) + Tailwind CSS (v4)
- **AI Integration:** Google Gemini API (Sử dụng package `Mscc.GenerativeAI` hoặc `System.Net.Http` để gọi API trực tiếp)

### Database Schema (PostgreSQL)

```sql
-- Bảng Users
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(50) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- Bảng ChatRooms
CREATE TABLE chat_rooms (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description VARCHAR(255),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- Bảng Messages
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

## 📁 Proposed File Structure

```plaintext
NexivraChat/
├── backend/
│   └── NexivraChatBackend/
│       ├── Controllers/
│       │   ├── AuthController.cs          # Đăng ký & Đăng nhập
│       │   └── RoomsController.cs         # CRUD Room & Lấy lịch sử chat
│       ├── Hubs/
│       │   └── ChatHub.cs                 # SignalR Hub quản lý kết nối & stream AI
│       ├── Models/
│       │   ├── User.cs                    # Cập nhật PasswordHash
│       │   ├── ChatRoom.cs                # Model phòng chat
│       │   └── Message.cs                 # Cập nhật IsAi, RoomId, SenderName
│       ├── Repositories/
│       │   ├── UserRepository.cs          # Dapper CRUD User
│       │   ├── RoomRepository.cs          # Dapper CRUD Room
│       │   └── MessageRepository.cs       # Dapper CRUD Message (Sửa query khớp schema)
│       ├── Services/
│       │   ├── TokenService.cs            # Tạo JWT Token
│       │   └── AiService.cs               # Gọi Gemini API streaming
│       └── Program.cs                     # Cấu hình SignalR, JWT, CORS, DI
└── frontend/
    └── nexivra-chat-frontend/
        └── src/
            ├── components/
            │   ├── ChatWindow.tsx         # Khung hiển thị tin nhắn & Stream text
            │   ├── RoomSidebar.tsx        # Danh sách phòng chat
            │   └── CopilotPanel.tsx       # Bảng trợ lý AI (Smart suggestion & Tóm tắt)
            ├── views/
            │   ├── LoginView.tsx          # Trang đăng nhập / đăng ký
            │   └── ChatView.tsx           # Trang chat chính
            ├── services/
            │   ├── authService.ts         # Axios calls cho Auth
            │   └── signalrService.ts      # Quản lý kết nối SignalR
            └── App.tsx                    # Điều phối Router hoặc State View
```

---

## 📋 Task Breakdown

### Phase 1: Database Setup & Foundations (Priority: P0)
*Thực hiện bởi: `database-architect` | Kỹ năng: `database-design`*

#### Task 1.1: Tạo các bảng trên PostgreSQL
- **Mô tả:** Chạy Script SQL tạo bảng `users`, `chat_rooms`, và `messages` khớp với cấu trúc Database Schema.
- **INPUT:** Script SQL tạo bảng.
- **OUTPUT:** Các bảng được khởi tạo thành công trên PostgreSQL.
- **VERIFY:** Kết nối Database qua công cụ quản trị (như pgAdmin) và chạy lệnh `SELECT * FROM users;` không lỗi.

#### Task 1.2: Cập nhật các Class Models ở Backend
- **Mô tả:** Thêm `PasswordHash` vào `User.cs`. Thêm `IsAi` vào `Message.cs`. Cập nhật các trường khớp với SQL schema.
- **INPUT:** File `backend/NexivraChatBackend/Models/*.cs`.
- **OUTPUT:** Các Model Entity hoàn thiện thuộc tính.
- **VERIFY:** Build project backend không bị lỗi compile.

---

### Phase 2: Core Backend - API & Authentication (Priority: P1)
*Thực hiện bởi: `backend-specialist` | Kỹ năng: `api-patterns`*

#### Task 2.1: UserRepository & AuthController (Đăng ký/Đăng nhập)
- **Mô tả:** Viết hàm đăng ký (băm mật khẩu bằng BCrypt.Net) và đăng nhập (kiểm tra mật khẩu, tạo JWT).
- **INPUT:** `UserRepository.cs`, `AuthController.cs`.
- **OUTPUT:** API `/api/auth/register` và `/api/auth/login`.
- **VERIFY:** Gọi API qua Postman/HTTP file và nhận về JWT Token thành công.

#### Task 2.2: RoomRepository & RoomsController
- **Mô tả:** Viết repository lấy danh sách phòng và lấy lịch sử tin nhắn của một phòng chat theo dạng phân trang (limit, offset).
- **INPUT:** `RoomRepository.cs`, `RoomsController.cs`.
- **OUTPUT:** API `/api/rooms` và `/api/rooms/{id}/messages`.
- **VERIFY:** Gọi API lấy lịch sử trả về định dạng JSON đúng cấu trúc tin nhắn.

---

### Phase 3: SignalR Hub & AI Integration (Priority: P1.5)
*Thực hiện bởi: `backend-specialist` | Kỹ năng: `api-patterns`*

#### Task 3.1: Cấu hình ChatHub (SignalR)
- **Mô tả:** Xây dựng Hub xử lý kết nối, lưu trữ thông tin phòng chat (`AddToGroupAsync`), và phát tin nhắn đến phòng.
- **INPUT:** `Hubs/ChatHub.cs`.
- **OUTPUT:** SignalR endpoint `/chatHub` sẵn sàng hoạt động.
- **VERIFY:** Client kết nối thành công và nhận được tin nhắn test.

#### Task 3.2: AI Service & Streaming Token qua SignalR
- **Mô tả:** Viết Service gọi Gemini API (Streaming response). Khi nhận tin nhắn `@copilot`, Hub sẽ kích hoạt stream và gửi từng token về client qua SignalR: `Clients.Group(roomId).SendAsync("ReceiveAiToken", messageId, token)`. Khi kết thúc stream, lưu toàn bộ tin nhắn AI vào Database.
- **INPUT:** `Services/AiService.cs`, `Hubs/ChatHub.cs`.
- **OUTPUT:** Luồng stream AI tích hợp hoàn tất.
- **VERIFY:** Khi gửi tin nhắn tag `@copilot`, backend gọi Gemini và phát ra sự kiện gửi token liên tục.

---

### Phase 4: Frontend - UI Components & Authentication (Priority: P2)
*Thực hiện bởi: `frontend-specialist` | Kỹ năng: `frontend-design`*

#### Task 4.1: Login & Register Interface
- **Mô tả:** Giao diện đăng nhập/đăng ký sử dụng Ant Design. Lưu trữ JWT Token vào LocalStorage.
- **INPUT:** `src/views/LoginView.tsx`.
- **OUTPUT:** UI Đăng nhập đẹp mắt, hỗ trợ validation.
- **VERIFY:** Người dùng đăng nhập thành công và chuyển hướng tới ChatView.

#### Task 4.2: Room Sidebar & Chat Area
- **Mô tả:** Thiết kế Layout chính gồm Sidebar danh sách phòng và Khung chat ở giữa.
- **INPUT:** `src/views/ChatView.tsx`, `src/components/RoomSidebar.tsx`.
- **OUTPUT:** Bố cục chat hiện đại, hỗ trợ cuộn tin nhắn và hiển thị avatar/tên người gửi.
- **VERIFY:** Hiển thị đúng danh sách phòng từ API và tải tin nhắn lịch sử khi chọn phòng.

#### Task 4.3: Tích hợp SignalR Client & Xử lý Streaming tin nhắn
- **Mô tả:** Kết nối tới Hub qua `@microsoft/signalr`. Xử lý hiển thị tin nhắn thời gian thực của người dùng khác. Đối với tin nhắn AI, cập nhật mượt mà từng token nhận được vào tin nhắn đang hiển thị.
- **INPUT:** `src/services/signalrService.ts`, `src/components/ChatWindow.tsx`.
- **OUTPUT:** Tin nhắn người dùng hiển thị ngay lập tức; tin nhắn AI hiển thị hiệu ứng gõ chữ (streamed).
- **VERIFY:** Test mở 2 tab trình duyệt, chat qua lại thấy tin nhắn hiển thị tức thì. Gửi lệnh `@copilot` thấy chữ phản hồi chạy ra dần dần.

---

### Phase 5: AI Copilot Panel & UX Polish (Priority: P2.5)
*Thực hiện bởi: `frontend-specialist` | Kỹ năng: `frontend-design`*

#### Task 5.1: Bảng điều khiển trợ lý phụ (AI Assistant Panel)
- **Mô tả:** Thiết kế sidebar phụ bên phải hoặc nút gợi ý. Cung cấp chức năng "Tóm tắt cuộc trò chuyện hiện tại" hoặc "Gợi ý câu trả lời nhanh".
- **INPUT:** `src/components/CopilotPanel.tsx`.
- **OUTPUT:** UI Panel bổ trợ thông minh.
- **VERIFY:** Click nút "Tóm tắt" gửi request lên API backend và hiển thị bản tóm tắt từ AI.

---

## 🏁 Phase X: Final Verification Plan

### Automated Verification
Chạy bộ kịch bản kiểm tra chất lượng mã nguồn:
- **Lint & Types:** `npm run lint` & `npx tsc --noEmit` trong thư mục frontend.
- **Security Check:** Chạy script bảo mật `.agents/scripts/checklist.py` hoặc quét code.
- **Build Check:** Chạy `dotnet build` ở backend và `npm run build` ở frontend.

### Manual Verification
- [ ] Chạy song song Server Backend và Client Frontend.
- [ ] Đăng ký 2 tài khoản khác nhau trên 2 trình duyệt khác nhau.
- [ ] Cùng tham gia vào phòng "General". Chat qua lại xem tin nhắn có xuất hiện realtime không.
- [ ] Gõ `@copilot Hãy giải thích SignalR là gì ngắn gọn trong 2 câu`. Xác minh tin nhắn AI xuất hiện dạng gõ chữ (stream từng token) thời gian thực.
- [ ] Tắt và mở lại ứng dụng, xác minh lịch sử chat được load chính xác từ PostgreSQL.
- [ ] Đảm bảo phối màu ứng dụng không sử dụng mã màu Tím (Purple Ban).
