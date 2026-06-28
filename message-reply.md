# Plan: GĐ5.2 Reply / Quote (Trả lời trích dẫn tin nhắn)

Kế hoạch triển khai tính năng phản hồi và trích dẫn tin nhắn thời gian thực cho phòng chat công khai và trò chuyện riêng tư (DM).

## 📋 Mục tiêu & Quy tắc
1. **Database Idempotent**: Thêm cột `reply_to_id INT NULL REFERENCES messages(id) ON DELETE SET NULL` và index `idx_messages_reply_to` vào bảng `messages` trong `DbInitializer`.
2. **Backend Projection**: Bổ sung `ReplyToId`, `ReplyToSenderName`, `ReplyToContent` vào `Message` model. Thực hiện `LEFT JOIN messages r ON m.reply_to_id = r.id` trong `MessageRepository`.
3. **SignalR Instant Hydration**: Cập nhật `SendMessage` và `SendPrivateMessage` trong `ChatHub` nhận `int? replyToId = null`, nạp snapshot tin gốc để gán vào object broadcast thời gian thực.
4. **Giao diện (UI)**:
   - Thêm nút "Trả lời" trên `MessageBubble`.
   - Hiển thị thanh preview trích dẫn trên ô nhập liệu khi chọn trả lời (có nút ✕ để hủy).
   - Render khối quote với đường viền màu Teal `#0D9488` trên bong bóng chat, nhấp vào cuộn mượt tới tin nhắn gốc.

---

## 🏗️ Phân bổ Công việc Chi tiết

### Phase 1: Database & Backend Core
- **[DbInitializer.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Data/DbInitializer.cs)**: Thêm câu lệnh migration `ALTER TABLE messages ADD COLUMN IF NOT EXISTS reply_to_id...` và index `idx_messages_reply_to`.
- **[Message.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Models/Message.cs)**: Bổ sung 3 thuộc tính `ReplyToId`, `ReplyToSenderName`, `ReplyToContent`.
- **[MessageRepository.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Repositories/MessageRepository.cs)**:
  - Cập nhật `SaveNewMessage` thêm `reply_to_id`.
  - Cập nhật các hàm đọc tin nhắn (`GetMessagesByRoom`, `GetMessagesByPrivateChat` và các biến thể `beforeId`/`afterId`) với `LEFT JOIN` và projection tường minh.
  - Bổ sung hàm `GetById(int id)` phục vụ Hub lookup.

### Phase 2: SignalR Hub Updates
- **[ChatHub.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Hubs/ChatHub.cs)**:
  - Cập nhật `SendMessage` và `SendPrivateMessage` nhận `int? replyToId = null`.
  - Gán `userMessage.ReplyToId` và nạp snapshot tin gốc nếu `replyToId > 0` trước khi broadcast.

### Phase 3: Frontend Component & State Management
- **[MessageBubble.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/components/MessageBubble.tsx)**:
  - Thêm nút "Trả lời".
  - Gán `id={'msg-' + msg.id}` cho wrapper.
  - Render khối trích dẫn viền Teal `#0D9488` kèm tính năng click cuộn mượt (`scrollIntoView`).
- **[ChatView.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/views/ChatView.tsx)**:
  - Cập nhật interface `Message`.
  - Quản lý state `replyingTo`.
  - Render thanh preview trích dẫn phía trên `<Input>`.
  - Cập nhật hàm gửi tin nhắn SignalR truyền `replyToId`.

### Phase 4: Verification & Documentation
- **Kiểm thử**: `dotnet test` (Docker bật) & `npm run build`.
- **Checklist & Docs**: Chạy `checklist.py`, cập nhật `context.md` và `TODOS.md`.
- **Git Commit**: `feat(reply): trả lời trích dẫn tin nhắn`.
