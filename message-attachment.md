# Plan: GĐ5.5 Gửi Ảnh / File Đính Kèm (Message Attachments)

Kế hoạch thực thi tính năng đính kèm ảnh và tài liệu vào tin nhắn (cả nhóm & 1-1), thực thi nghiêm ngặt 5 nguyên tắc bảo mật Threat-Model.

## 📋 Mục tiêu & Quy tắc Bảo mật Threat-Model
1. **Database Schema**: Bổ sung 4 cột `attachment_url`, `attachment_name`, `attachment_type`, `attachment_size` vào bảng `messages` (idempotent migration).
2. **Threat-Model Item 1 (Kích thước ≤ 10MB)**: Giới hạn 10MB ở Client, `[RequestSizeLimit]` ở Controller và Kestrel.
3. **Threat-Model Item 2 (Whitelist & Anti-XSS)**: Chỉ nhận `jpg, jpeg, png, gif, webp, pdf`. **Tuyệt đối cấm SVG**. Check đuôi file & mime.
4. **Threat-Model Item 3 (Safe Filename & Anti-Path Traversal)**: Lưu đĩa với tên `{guid}{ext}`, tên gốc sanitize lưu ở `attachment_name`.
5. **Threat-Model Item 4 (Safe File Serving)**: `UseStaticFiles` với header `X-Content-Type-Options: nosniff` và `Content-Disposition: attachment` cho file tài liệu.
6. **Threat-Model Item 5 (Auth)**: Endpoint upload yêu cầu JWT `[Authorize]`.

---

## 🏗️ Phân bổ Công việc Chi tiết

### Phase 1: Backend Core & Security Layer
- **[DbInitializer.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Data/DbInitializer.cs)**: Migration bổ sung 4 cột `attachment_*` cho bảng `messages`.
- **[Message.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Models/Message.cs)** & **[MessageRepository.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Repositories/MessageRepository.cs)**: Cập nhật C# model và tất cả các câu SQL SELECT/INSERT.
- **[UploadController.cs (New)](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Controllers/UploadController.cs)**: Tạo Controller tiếp nhận file upload thực thi đủ 5 quy tắc Threat-Model.
- **[Program.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Program.cs)**: Cấu hình `app.UseStaticFiles()` kèm các header an toàn HTTP.

### Phase 2: SignalR Hub Integration
- **[ChatHub.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Hubs/ChatHub.cs)**: Thêm các tham số đính kèm optional vào `SendMessage` & `SendPrivateMessage`, gán dữ liệu và broadcast.

### Phase 3: Frontend Component & State Management
- **[ChatView.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/views/ChatView.tsx)**: Thêm nút kẹp giấy chọn file, validate client, gọi API upload, hiển thị thanh xem trước đính kèm và truyền metadata qua SignalR.
- **[MessageBubble.tsx](file:///d:/Vibe_Coding/NexivraChat/frontend/nexivra-chat-frontend/src/components/MessageBubble.tsx)**: Render ảnh inline với AntD `<Image preview>` hoặc hiển thị chip tải về cho file tài liệu.

### Phase 4: Verification & Documentation
- **Kiểm thử**: `dotnet test` (Docker bật) & `npm run build`.
- **Checklist & Docs**: Chạy `checklist.py`, cập nhật `context.md` và `TODOS.md`.
- **Git Commit**: `feat(attachment): gửi ảnh và file đính kèm`.
