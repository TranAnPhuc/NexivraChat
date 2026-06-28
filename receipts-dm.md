# Plan: GĐ4.5 Trạng thái tin nhắn Đã gửi / Đã xem cho DM

Mô tả kế hoạch triển khai tính năng hiển thị trạng thái tin nhắn "Đã gửi" / "Đã xem" cho các cuộc trò chuyện cá nhân (DM), tái sử dụng bảng `conversation_reads` và luồng SignalR.

## 📋 Mục tiêu & Nguyên tắc
1. **Không tạo bảng mới**: Dựa trên cột `conversation_reads.last_read_message_id` của đối phương trong cuộc hội thoại 1-1. Tin `m` được coi là "Đã xem" nếu `last_read_message_id >= m.id`.
2. **Trạng thái "Đã gửi"**: Đã lưu DB thành công (`SaveNewMessage` trả về ID dương).
3. **Phạm vi hiển thị**: Chỉ hiển thị dưới tin nhắn mới nhất do chính mình gửi trong DM (kiểu Messenger/Zalo).
4. **Realtime**: Khi người nhận mở/xem tin nhắn (`MarkRead`), phát SignalR event `SeenUpdate` tới người gửi để cập nhật UI ngay lập tức.
5. **Đồng bộ khi mở DM**: Khi người gửi tải lịch sử chat (`GET /api/users/private-chat/{id}/messages`), nhận về mốc `partnerLastReadMessageId` để hiển thị chính xác trạng thái cũ.

---

## 🏗️ Phân bổ Công việc Chi tiết

### Phase 1: Backend Database & Repositories
- **[ConversationReadRepository.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Repositories/ConversationReadRepository.cs)**:
  - Bổ sung phương thức `GetPartnerLastReadMessageId(int userId, int partnerUserId)` truy vấn `conversation_reads` dựa trên `private_chats` giữa 2 user.

### Phase 2: Backend REST Controllers & SignalR Hub
- **[UsersController.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Controllers/UsersController.cs)**:
  - Cập nhật endpoint `GET /api/users/private-chat/{id}/messages` để lấy `partnerLastReadMessageId` và đính kèm vào Response Header `X-Partner-Last-Read-Id`.
- **[Program.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Program.cs)**:
  - Cấu hình CORS expose header `X-Partner-Last-Read-Id`.
- **[ChatHub.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Hubs/ChatHub.cs)**:
  - Trong phương thức `MarkRead` đối với DM, sau khi update `conversation_reads`, phát SignalR event `SeenUpdate` tới người gửi:
    `await Clients.User(otherUserId.ToString()).SendAsync("SeenUpdate", new { privateChatUserId = me, lastReadMessageId });`

### Phase 3: Frontend UI Components & State Management
- **[ChatView.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/views/ChatView.tsx)**:
  - Bổ sung state `partnerLastReadId` cho hội thoại DM đang mở.
  - Cập nhật `fetchPrivateMessageHistory` đọc header `x-partner-last-read-id` để gán vào state `partnerLastReadId`.
  - Lắng nghe SignalR event `SeenUpdate`: Nếu `payload.privateChatUserId === activeRecipientId`, cập nhật `partnerLastReadId = payload.lastReadMessageId`.
  - Tính toán ID tin nhắn cuối cùng do chính mình gửi (`latestMyMessageId`).
  - Truyền prop `receiptStatus` (`'sent'` hoặc `'seen'`) cho `MessageBubble`.
- **[MessageBubble.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/components/MessageBubble.tsx)**:
  - Tiếp nhận prop `receiptStatus?: 'sent' | 'seen'`.
  - Render nhãn bên dưới tin nhắn nếu có `receiptStatus`: "✓ Đã gửi" (màu xám dịu) hoặc "✓✓ Đã xem" (màu Teal `#0D9488`).

---

## 🧪 Quy trình Kiểm thử & Xã nhận
1. **Chạy Test Backend**: `dotnet test` (đảm bảo 13+ integration tests pass).
2. **Kiểm tra Biên dịch Frontend**: `npm run build` (không lỗi kiểu dữ liệu TypeScript).
3. **Chạy Master Checklist**: `python .agents/scripts/checklist.py .` với `PYTHONIOENCODING="utf-8"`.
4. **Cập nhật & Commit**: Update `context.md` và `TODOS.md`, commit `feat(receipts): Đã gửi/Đã xem cho DM`.
