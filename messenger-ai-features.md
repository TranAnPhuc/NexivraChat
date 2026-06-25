# Lộ trình phát triển NexivraChat: Messenger tích hợp AI

Lộ trình này chia nhỏ quá trình nâng cấp NexivraChat từ một ứng dụng chat nhóm đơn giản thành một nền tảng Messenger-like thương mại hóa tích hợp AI. Kế hoạch ưu tiên chất lượng mã nguồn, trải nghiệm người dùng cao cấp (Premium UI/UX) và tính mở rộng dài hạn.

## Project Type
- **WEB / BACKEND** (React 19 + .NET 8 Web API + PostgreSQL Dapper)

## Success Criteria
- Hệ thống hỗ trợ chat 1-1 riêng tư thời gian thực bên cạnh chat nhóm.
- Tích hợp AI dịch thuật tin nhắn trực tiếp với độ trễ cực thấp.
- Trang Profile cá nhân hiện đại chứa các thẻ phân tích tính cách, phong cách giao tiếp bằng AI.
- Giao diện responsive, mượt mà và tương thích tốt trên cả mobile và desktop.
- Vượt qua các bước kiểm tra chất lượng từ bộ công cụ validation (`ux_audit.py`, `vulnerability_scan.py`).

---

## Phân chia các Giai đoạn phát triển (Phased Roadmap)

### Giai đoạn 1: Nền tảng Messenger (Chat 1-1 & Trạng thái hoạt động)
Xây dựng nền tảng nhắn tin riêng tư 1-1 giữa các thành viên.

#### [NEW] [private-chats-schema](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Data/DbInitializer.cs)
- Tạo bảng `private_chats` để lưu thông tin cuộc hội thoại 1-1.
- Cập nhật bảng `messages` để hỗ trợ khóa ngoại `private_chat_id` (nullable).

#### [MODIFY] [ChatHub.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Hubs/ChatHub.cs)
- Thêm cơ chế gửi tin nhắn riêng tư `SendPrivateMessage(receiverId, content)`.
- Đồng bộ sự kiện realtime qua SignalR đến kết nối của người nhận.

#### [MODIFY] [ChatView.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/views/ChatView.tsx)
- Cập nhật Sidebar để chuyển đổi mượt mà giữa danh sách phòng nhóm và danh sách chat 1-1.
- Hiển thị chấm tròn xanh trạng thái Online/Offline trực quan của bạn bè trong danh sách.

---

### Giai đoạn 2: Trợ lý dịch tin nhắn Real-time bằng AI
Tích hợp tính năng tự động hoặc thủ công dịch tin nhắn bằng Gemini API.

#### [NEW] [TranslationService.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Services/TranslationService.cs)
- Xây dựng service gọi Gemini API để dịch đoạn văn bản sang ngôn ngữ đích (ví dụ: Tiếng Anh, Tiếng Việt, Tiếng Nhật).

#### [MODIFY] [MessagesController.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Controllers/RoomsController.cs) (hoặc Controller mới)
- Thêm API `/api/messages/{id}/translate` cho phép dịch nhanh tin nhắn theo yêu cầu.

#### [MODIFY] [ChatView.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/views/ChatView.tsx)
- Bổ sung nút dịch (biểu tượng quốc kỳ hoặc icon dịch thuật) dưới mỗi bubble chat.
- Khi bấm, nội dung dịch sẽ xuất hiện mượt mà ngay dưới tin nhắn gốc dạng accordion hoặc tooltip cao cấp.

---

### Giai đoạn 3: AI Profile Analyzer (Phân tích phong cách cá nhân)
Tạo trang Profile cá nhân và tích hợp AI phân tích dữ liệu chat để chỉ ra phong cách giao tiếp, thói quen và tính cách.

#### [NEW] [ProfileView.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/views/ProfileView.tsx)
- Thiết kế màn hình xem Profile của bản thân và người khác.
- Hiển thị thông tin cơ bản: ảnh đại diện, tiểu sử, ngôn ngữ bản địa.
- Hiển thị các khối thông tin AI phân tích: phong cách giao tiếp, thói quen, biểu đồ radar tính cách giao tiếp.

#### [NEW] [ProfileRepository.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Repositories/ProfileRepository.cs) & [Profile.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Models/Profile.cs)
- Lưu trữ kết quả phân tích AI dạng JSON trong Postgres.

#### [NEW] [ProfileController.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Controllers/ProfileController.cs)
- API chạy phân tích: Lấy 30 tin nhắn gần nhất của user, gửi prompt chuyên sâu cho Gemini để đánh giá tính cách, lưu trữ kết quả phân tích và trả về Frontend.

---

### Giai đoạn 4: Tối ưu hóa thương mại hóa (Monetization & Polish)
Tối ưu hóa sâu sắc và chuẩn bị cơ sở cho việc thu phí/Premium hóa sản phẩm.

#### [MODIFY] [ProfileController.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Controllers/ProfileController.cs)
- Thêm giới hạn lượt phân tích AI mỗi ngày (Rate Limit) cho tài khoản thường.
- Thiết kế giả lập thanh toán nâng cấp tài khoản VIP/Premium để mở khóa phân tích vô hạn và các ngôn ngữ dịch cao cấp.

#### [MODIFY] [Polish UI/UX](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/index.css)
- Làm mịn các hiệu ứng chuyển trang, micro-animations khi tin nhắn mới bay vào hoặc khi AI đang dịch.

---

## Task Breakdown cho Giai đoạn 1 (Task đầu tiên cần làm)

### Task 1.1: Tạo cấu trúc cơ sở dữ liệu cho Chat 1-1
- **Agent**: `database-architect`
- **Skill**: `database-design`
- **Priority**: P0
- **Dependencies**: Không có
- **INPUT**: Cấu trúc DB hiện tại.
- **OUTPUT**: Chạy migration/SQL để tạo bảng `private_chats` và liên kết khóa ngoại trong bảng `messages`.
- **VERIFY**: Kiểm tra bằng câu lệnh SELECT cột mới trong table `messages`.

### Task 1.2: Xây dựng API và SignalR Hub cho Chat 1-1
- **Agent**: `backend-specialist`
- **Skill**: `api-patterns`
- **Priority**: P0
- **Dependencies**: Task 1.1
- **INPUT**: [ChatHub.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Hubs/ChatHub.cs)
- **OUTPUT**: Cập nhật SignalR Hub hỗ trợ phương thức nhắn tin cá nhân.
- **VERIFY**: Gửi tin nhắn giữa hai kết nối khác nhau và xác nhận nhận được sự kiện real-time.

### Task 1.3: Cập nhật giao diện Sidebar và Hội thoại 1-1 ở Frontend
- **Agent**: `frontend-specialist`
- **Skill**: `frontend-design`
- **Priority**: P1
- **Dependencies**: Task 1.2
- **INPUT**: [RoomSidebar.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/components/RoomSidebar.tsx)
- **OUTPUT**: Sidebar cho phép chọn người dùng cụ thể để nhắn tin riêng biệt.
- **VERIFY**: Đăng nhập bằng 2 tài khoản trên 2 trình duyệt, click chọn nhau và chat thử trực tiếp.

---

## Phase X: Verification
- [ ] Chạy kiểm tra tĩnh (`npm run lint` & `dotnet build`)
- [ ] Chạy công cụ kiểm thử tự động (`python .agents/scripts/checklist.py`)
- [ ] Xác minh tính năng chat 1-1 hoạt động real-time
- [ ] Xác minh AI dịch tin nhắn phản hồi chính xác ngôn ngữ đích
- [ ] Xác minh AI phân tích Profile sinh ra dữ liệu tính cách hợp lý

## ✅ PHASE X COMPLETE
- Lint: [ ]
- Security: [ ]
- Build: [ ]
- Date: 2026-06-24
