# Plan: GĐ5.4 @mention Nhắc tên người dùng trong phòng chat (Mention Users)

Kế hoạch triển khai tính năng gõ `@username` nhắc tên thành viên trong các phòng chat công khai, lưu trữ bền vững trên Database và thông báo thời gian thực qua SignalR.

## 📋 Mục tiêu & Quy tắc
1. **Database Migration**: Tạo bảng `message_mentions (message_id, mentioned_user_id)` với khóa chính kép và index tối ưu trong `DbInitializer` (idempotent).
2. **Backend Parser & Persistence**: Parse regex `@([A-Za-z0-9_]+)` trong `SendMessage` (chỉ áp dụng cho Room). Loại bỏ chính người gửi và `@copilot`. Lưu DB qua `MentionRepository` và phát sự kiện `MentionUpdate` thời gian thực.
3. **Rest API & Unread Tracker**: Endpoint `GET /api/users/mentions` trả về danh sách room ID chứa tin nhắn nhắc tên chưa đọc.
4. **Giao diện Frontend (UI/UX)**:
   - Autocomplete dropdown gợi ý thành viên khi gõ `@`.
   - Highlight token `@username` tô màu Teal `#0D9488` trong `MessageBubble`. Thêm viền nổi bật nếu chính mình được nhắc.
   - Hiển thị chỉ báo nhãn `@` bên cạnh tên phòng ở sidebar và hiển thị toast notification thời gian thực khi phòng nằm ở nền.

---

## 🏗️ Phân bổ Công việc Chi tiết

### Phase 1: Database & Repository Layer
- **[DbInitializer.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Data/DbInitializer.cs)**: Bổ sung SQL script khởi tạo bảng `message_mentions` và index `idx_mentions_user`.
- **[UserRepository.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Repositories/UserRepository.cs)**: Thêm phương thức `GetByUsernames(IEnumerable<string> usernames)`.
- **[MentionRepository.cs (New)](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Repositories/MentionRepository.cs)**: Thêm mới repository quản lý `SaveMentions` và `GetUnreadMentionRoomIds`.
- **[Program.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Program.cs)**: Đăng ký `MentionRepository` vào DI container.

### Phase 2: Controller & SignalR Hub Updates
- **[UsersController.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Controllers/UsersController.cs)**: Bổ sung endpoint `GET /api/users/mentions`.
- **[ChatHub.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Hubs/ChatHub.cs)**: Nâng cấp `SendMessage` để parse mention, lưu DB và phát event `MentionUpdate` tới từng user được nhắc.

### Phase 3: Frontend Component & State Management
- **[MessageBubble.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/components/MessageBubble.tsx)**: Phân tách và tô màu Teal `#0D9488` cho token `@username`, làm nổi bật khi bản thân được nhắc.
- **[ChatView.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/views/ChatView.tsx)**: Thêm autocomplete popup gợi ý user khi gõ `@`, nạp danh sách `mentionRooms` từ API, lắng nghe SignalR `MentionUpdate` để bật toast notification và cập nhật badge.
- **[RoomSidebar.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/components/RoomSidebar.tsx)**: Hiển thị chỉ báo `@` đỏ/teal bên cạnh các phòng có mention chưa đọc.

### Phase 4: Verification & Documentation
- **Kiểm thử**: `dotnet test` (Docker bật) & `npm run build`.
- **Checklist & Docs**: Chạy `checklist.py`, cập nhật `context.md` và `TODOS.md`.
- **Git Commit**: `feat(mention): nhắc tên @user trong phòng`.
