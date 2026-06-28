# Plan: GĐ5.3 Sửa / Xóa tin nhắn (Edit & Soft Delete Messages)

Kế hoạch triển khai tính năng sửa nội dung và thu hồi (xóa mềm) tin nhắn thời gian thực cho phòng chat công khai và trò chuyện riêng tư (DM).

## 📋 Mục tiêu & Quy tắc
1. **Database Migration**: Thêm 2 cột `edited_at TIMESTAMP NULL` và `deleted_at TIMESTAMP NULL` trong `DbInitializer` (idempotent).
2. **Bảo mật Nội dung**: Tất cả các câu SELECT SQL trong `MessageRepository` bọc `CASE WHEN m.deleted_at IS NOT NULL THEN '' ELSE m.content END AS Content` và snapshot reply `CASE WHEN r.deleted_at IS NOT NULL THEN NULL ELSE LEFT(r.content, 120) END AS ReplyToContent`. Nội dung tin nhắn đã xóa tuyệt đối không rò rỉ ra API.
3. **Phân quyền Server-Side**: `EditMessage` và `SoftDeleteMessage` trong repository chỉ cập nhật khi `sender_id = @userId`. Hub ném `HubException` nếu `affected == 0`.
4. **SignalR Broadcast**: Phát sự kiện `MessageEdited` và `MessageDeleted` tới các thành viên hội thoại.
5. **Giao diện (UI)**: Nút Sửa/Xóa chỉ hiện cho tin nhắn của chính mình (chưa xóa, không AI, id>0). Tích hợp inline editor, nhãn `(đã sửa)`, và tombstone *"Tin đã bị xóa"* cho tin nhắn bị thu hồi.

---

## 🏗️ Phân bổ Công việc Chi tiết

### Phase 1: Database & Backend Core
- **[DbInitializer.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Data/DbInitializer.cs)**: Thêm SQL migration `ALTER TABLE messages ADD COLUMN IF NOT EXISTS edited_at...` và `deleted_at...`.
- **[Message.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Models/Message.cs)**: Bổ sung `EditedAt` và `DeletedAt`.
- **[MessageRepository.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Repositories/MessageRepository.cs)**:
  - Bọc bảo vệ nội dung tin nhắn đã xóa ở tất cả các hàm SELECT.
  - Thêm `EditMessage(messageId, userId, newContent)` và `SoftDeleteMessage(messageId, userId)`.

### Phase 2: SignalR Hub Updates
- **[ChatHub.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Hubs/ChatHub.cs)**:
  - Thêm SignalR method `EditMessage(int messageId, string newContent)` và `DeleteMessage(int messageId)`.
  - Thực thi kiểm tra authz qua `affected == 0` và phát sự kiện `MessageEdited` / `MessageDeleted`.

### Phase 3: Frontend Component & State Management
- **[ChatView.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/views/ChatView.tsx)**:
  - Cập nhật interface `Message` thêm `editedAt` và `deletedAt`.
  - Lắng nghe SignalR event `MessageEdited` và `MessageDeleted` để cập nhật state.
  - Thêm callback `handleEditMessage` và `handleDeleteMessage`.
- **[MessageBubble.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/components/MessageBubble.tsx)**:
  - Render nút Sửa/Xóa cho tin của chính mình.
  - Tích hợp inline editor (mode chỉnh sửa).
  - Render nhãn `(đã sửa)` bên cạnh giờ gửi.
  - Render tombstone *"Tin đã bị xóa"* mờ nhạt và ẩn các thao tác khi tin đã bị thu hồi.

### Phase 4: Verification & Documentation
- **Kiểm thử**: `dotnet test` (Docker bật) & `npm run build`.
- **Checklist & Docs**: Chạy `checklist.py`, cập nhật `context.md` và `TODOS.md`.
- **Git Commit**: `feat(edit-delete): sửa và thu hồi tin nhắn`.
