# Plan: Hardening #C Kiểm Soát Truy Cập File & Mention Repository Tests

Kế hoạch chuyển đổi cơ chế phục vụ file đính kèm sang endpoint xác thực bảo mật và viết bộ integration test cho MentionRepository.

## 📋 Mục tiêu Kỹ thuật & Bảo mật
1. **Lưu trữ bảo mật**: Chuyển vị trí lưu file upload ra ngoài wwwroot (`{ContentRootPath}/uploads/{yyyy}/{MM}/{guid}{ext}`).
2. **Endpoint Phục vụ có Phân quyền**: Tạo `FilesController.cs` `[Authorize]` truy cập `GET /api/files/{year}/{month}/{filename}`. Kiểm tra participant DM (trả về `403 Forbidden` nếu không phải người trong cuộc), hỗ trợ `.webp -> image/webp` và ép tải về đối với file tài liệu.
3. **Truyền Token cho Static Asset**: Nâng cấp `JwtBearerEvents.OnMessageReceived` trong `Program.cs` hỗ trợ `access_token` qua query string cho `/api/files`.
4. **Phủ Test Integration Mention**: Tạo `MentionRepositoryTests.cs` phủ 2 trường hợp test cho `GetUnreadMentionRoomIds`.

---

## 🏗️ Phân bổ Công việc Chi tiết

### Phase 1: Backend Secure File Serving & Repository Updates
- **[UploadController.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Controllers/UploadController.cs)**: Đổi thư mục lưu trữ sang `ContentRootPath` và cập nhật định dạng URL trả về `/api/files/...`.
- **[MessageRepository.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Repositories/MessageRepository.cs)**: Bổ sung phương thức `GetByAttachmentUrl(string url)`.
- **[FilesController.cs (New)](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Controllers/FilesController.cs)**: Tạo mới Controller kiểm soát truy cập file, sanitize tham số chống path traversal, verify participant DM.
- **[Program.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Program.cs)**: Cập nhật `JwtBearerEvents` chấp nhận `access_token` query string cho đường dẫn `/api/files`.

### Phase 2: Frontend Integration
- **[MessageBubble.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/components/MessageBubble.tsx)**: Gắn `?access_token={token}` vào thẻ `<Image src>` và thẻ `<a href>` tải file.

### Phase 3: Integration Tests
- **[MentionRepositoryTests.cs (New)](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend.Tests/Integration/MentionRepositoryTests.cs)**: Tạo mới file test phủ 2 cases cho `GetUnreadMentionRoomIds`.

### Phase 4: Verification & Commits
- **Kiểm thử**: `dotnet test` (xanh lá 34 tests) & `npm run build`.
- **Checklist & Docs**: Tick `#C` + `GĐ5.4-test` trong `TODOS.md`, cập nhật `context.md`.
- **2 Git Commits riêng biệt**:
  1. `fix(attachment): kiểm soát truy cập file qua endpoint có auth`
  2. `test: phủ GetUnreadMentionRoomIds`
