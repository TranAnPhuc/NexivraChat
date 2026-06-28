# Plan: Dọn Nợ Kỹ Thuật — Đợt 1 (#2, #3, #D)

Kế hoạch thực hiện dọn nợ kỹ thuật đợt 1 gồm 3 nhiệm vụ làm sạch mã nguồn và tăng cường bảo mật.

## 📋 Mục tiêu Kỹ thuật & Bảo mật
1. **Receipts theo SenderId (#2)**: Xác định tin nhắn cá nhân trong DM qua `senderId === currentUserId` thay vì so sánh tên `senderName === username`.
2. **Loại bỏ Dead Code (#3)**: Xóa hoàn toàn phương thức `MessageRepository.GetOldMessages` không còn sử dụng.
3. **Magic-bytes Verification (#D)**: Kiểm tra chữ ký file thực tế (header magic bytes) trong `UploadController` để chống giả mạo đuôi file.

---

## 🏗️ Phân bổ Công việc Chi tiết & Commits

### Task 1: Refactor Receipts theo senderId
- **[ChatView.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/views/ChatView.tsx)**: Đổi logic so sánh xác định tin nhắn riêng tư DM của chính mình ở vòng lặp `latestMyId`.
- **Commit 1**: `refactor(receipts): xác định tin của mình theo senderId`

### Task 2: Dead Code Cleanup
- **[MessageRepository.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Repositories/MessageRepository.cs)**: Xóa hàm `GetOldMessages`.
- **[TODOS.md](file:///d:/Vibe_Coding/NexivraChat/TODOS.md)** & **[context.md](file:///d:/Vibe_Coding/NexivraChat/context.md)**: Xóa ghi chú "dead code" ở mục kỹ thuật tồn.
- **Commit 2**: `chore: xóa GetOldMessages dead code`

### Task 3: Magic-Bytes Verification
- **[UploadController.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Controllers/UploadController.cs)**: Đọc ngắt 12 bytes đầu tiên qua stream để kiểm tra magic bytes của JPEG, PNG, GIF, WEBP, PDF.
- **Commit 3**: `fix(upload): kiểm magic-bytes chống giả định dạng`

### Verification & Checklists
- Biên dịch: `dotnet build` & `npm run build`.
- System Tests: `dotnet test` (34 tests passed).
- Tick các mục `#2`, `#3`, `#D` trong `TODOS.md`.
