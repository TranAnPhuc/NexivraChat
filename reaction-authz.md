# Plan: Kiểm tra participant cho Reactions cho DM (Authz)

Kế hoạch bổ sung kiểm tra phân quyền xem và thả reaction trên các tin nhắn trò chuyện riêng tư (DM), ngăn chặn người dùng không thuộc cuộc hội thoại tiếp cận hoặc thao tác cảm xúc.

## 📋 Mục tiêu & Quy tắc
1. **REST API Filtering**: Trong `ReactionRepository.GetReactionsForMessages`, JOIN bảng `messages` và lọc chỉ trả về reaction của tin thuộc phòng công khai hoặc DM mà `currentUserId` là thành viên.
2. **SignalR Hub Authorization**: Trong `ChatHub.ToggleReaction`, thực hiện kiểm tra phân quyền trước khi thực thi toggle và broadcast. Nếu user không thuộc DM, ném `HubException("Bạn không có quyền với hội thoại này.")`.
3. **Quy ước Repo**: Sử dụng Dapper async, liệt kê cột tường minh.
4. **Kiểm thử**: Chạy `dotnet test` (với Docker) xanh lá, tick `#A` trong `TODOS.md`, commit `fix(reactions): kiểm tra participant cho DM (authz)`.

---

## 🏗️ Phân bổ Công việc Chi tiết

### Phase 1: Backend Repository Authorization Update
- **[ReactionRepository.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Repositories/ReactionRepository.cs)**:
  - Cập nhật SQL trong `GetReactionsForMessages` kết hợp JOIN `messages` và EXISTS subquery kiểm tra quyền `currentUserId` trên `private_chats`.

### Phase 2: SignalR Hub Authorization Guard
- **[ChatHub.cs](file:///d:/Vibe_Coding/NexivraChat/backend/NexivraChatBackend/Hubs/ChatHub.cs)**:
  - Cập nhật method `ToggleReaction`: Gọi `LookupConversation` trước, kiểm tra xem nếu là DM thì xác minh membership via `_privateChatRepository.GetById`. Ném `HubException` nếu không có quyền trước khi gọi `ToggleReaction`.

### Phase 3: Verification & Documentation
- **Kiểm thử**: Chạy `dotnet test`.
- **Todos & Docs**: Tick `#A` trong `TODOS.md`, cập nhật `context.md`.
- **Git Commit**: `fix(reactions): kiểm tra participant cho DM (authz)`.
