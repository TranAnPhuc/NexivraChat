# Plan: GĐ5.6 — Tìm Kiếm Tin Nhắn Trong Hội Thoại & Search Tests

Kế hoạch triển khai tính năng tìm kiếm tin nhắn theo từ khóa trong hội thoại đang mở (phòng chat & DM) và viết bộ integration tests kiểm thử.

## 📋 Mục tiêu Kỹ thuật & Bảo mật
1. **Repository Search Methods**: Bổ sung `SearchRoomMessages` và `SearchPrivateChatMessages` trong `MessageRepository.cs` với bộ cột SELECT chuẩn và xử lý an toàn escape ký tự wildcard ILIKE (`\`, `%`, `_`).
2. **Endpoints & DM Authorization**: Thêm đường dẫn tìm kiếm trong `RoomsController` và `UsersController` (kiểm tra phân quyền participant DM nghiêm ngặt).
3. **Frontend UI & Jump Logic**: Thêm nút kính lúp tìm kiếm ở header, debounce 350ms, hiển thị kết quả và hỗ trợ cuộn mượt (`scrollIntoView`) + highlight nhấp nháy 2s khi bấm vào kết quả.
4. **Integration Tests**: Viết `MessageSearchTests.cs` phủ 3 kịch bản kiểm thử tích hợp.

---

## 🏗️ Phân bổ Công việc Chi tiết & Commits

### Phase 1: Backend Repository & Controllers (Commit 1)
- **[MessageRepository.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Repositories/MessageRepository.cs)**: Thêm hàm `SearchRoomMessages` và `SearchPrivateChatMessages`.
- **[RoomsController.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Controllers/RoomsController.cs)** & **[UsersController.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Controllers/UsersController.cs)**: Thêm 2 endpoints search.
- **[ChatView.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/views/ChatView.tsx)** & **[MessageBubble.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/components/MessageBubble.tsx)**: Thêm nút kính lúp, ô search debounce, panel kết quả, logic cuộn + highlight và prop `highlightedMsgId`.
- **Commit 1**: `feat(search): tìm kiếm tin nhắn trong hội thoại`

### Phase 2: Integration Tests & Documentation (Commit 2)
- **[MessageSearchTests.cs (New)](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend.Tests/Integration/MessageSearchTests.cs)**: Tạo file kiểm thử với 3 case: case-insensitive, loại bỏ tin đã xóa, escape wildcard `%` literal.
- **[TODOS.md](file:///d:/Vibe_Coding/NexivraChat/TODOS.md)** & **[context.md](file:///d:/Vibe_Coding/NexivraChat/context.md)**: Tick `GĐ5.6` + `5.6-test` và cập nhật các endpoint search mới.
- **Commit 2**: `test: phủ tìm kiếm tin nhắn`

### Verification
- Biên dịch & test: `dotnet build`, `dotnet test` (37 tests passed), `npm run build`.
