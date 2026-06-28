# Plan: GĐ5.1 Reactions (emoji) trên tin nhắn

Kế hoạch triển khai tính năng thả và gỡ cảm xúc (emoji) trên tin nhắn thời gian thực cho phòng chat và cuộc trò chuyện cá nhân (DM).

## 📋 Mục tiêu & Quy tắc
1. **Whitelist Emoji**: Chỉ chấp nhận 6 emoji tiêu chuẩn: `👍`, `❤️`, `😂`, `😮`, `😢`, `🙏`.
2. **Cơ chế Toggle**: Bấm thả emoji $\rightarrow$ Thả cảm xúc. Bấm lại cùng emoji $\rightarrow$ Gỡ cảm xúc.
3. **Database Idempotent**: Tạo bảng `message_reactions` với PK `(message_id, user_id, emoji)` trong `DbInitializer`.
4. **Realtime Broadcast**: Sử dụng SignalR phát sự kiện `ReactionUpdate` tới toàn bộ người dùng trong phòng hoặc DM.
5. **Giao diện (UI)**: Thẻ/Chip reaction dưới tin nhắn hiển thị `emoji + count`. Thẻ của mình thả có viền màu Teal `#0D9488`.

---

## 🏗️ Phân bổ Công việc Chi tiết

### Phase 1: Database & Backend Core
- **[DbInitializer.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Data/DbInitializer.cs)**: Tạo bảng `message_reactions` và index `idx_reactions_message`.
- **Models & DTOs**:
  - `Models/MessageReaction.cs`
  - `Models/ReactionSummary.cs` (`MessageId`, `Emoji`, `Count`, `MineReacted`)
- **[ReactionRepository.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Repositories/ReactionRepository.cs)** (Tạo mới):
  - `ToggleReaction(messageId, userId, emoji)` $\rightarrow$ trả về `(bool reacted, int newCount)`.
  - `GetReactionsForMessages(IEnumerable<int> messageIds, int currentUserId)` $\rightarrow$ trả về `List<ReactionSummary>`.
  - `LookupConversation(messageId)` $\rightarrow$ trả về `(int? roomId, int? privateChatId)`.
- **[Program.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Program.cs)**: Đăng ký `ReactionRepository` vào DI container.

### Phase 2: REST API & SignalR Hub
- **[ReactionsController.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Controllers/ReactionsController.cs)** (Tạo mới):
  - Endpoint `GET /api/reactions?messageIds=1,2,3` nạp reaction theo lô khi tải lịch sử.
- **[ChatHub.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Hubs/ChatHub.cs)**:
  - Bổ sung method `ToggleReaction(int messageId, string emoji)`.
  - Validate whitelist emoji, thực thi toggle và phát SignalR event `ReactionUpdate` `{ messageId, emoji, count, userId, reacted }`.

### Phase 3: Frontend Component & State Management
- **[ChatView.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/views/ChatView.tsx)**:
  - Trích xuất `currentUserId` từ JWT token.
  - State `reactions: Record<number, ReactionSummary[]>`.
  - Tải reaction khi fetch tin nhắn phòng/DM và khi load tin cũ.
  - Lắng nghe sự kiện `ReactionUpdate` để cập nhật state realtime.
- **[MessageBubble.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/components/MessageBubble.tsx)**:
  - Bổ sung giao diện chọn emoji (Picker) khi hover/long-press.
  - Render danh sách chip reaction bên dưới bong bóng chat, đánh dấu viền màu Teal `#0D9488` cho emoji của mình.

### Phase 4: Verification & Documentation
- **Kiểm thử**: `dotnet test` (với Docker bật) & `npm run build`.
- **Checklist & Docs**: Chạy `checklist.py`, cập nhật `context.md` và `TODOS.md`.
- **Git Commit**: `feat(reactions): thả emoji cảm xúc lên tin nhắn`.
