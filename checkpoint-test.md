# Plan: Checkpoint Test — Phủ test cho GĐ4.2 → GĐ5.3

Kế hoạch phủ 19 integration tests toàn diện cho các tính năng từ GĐ4.2 đến GĐ5.3, nâng tổng số test suite từ 13 lên 32 test.

## 📋 Mục tiêu & Quy tắc
1. **Kiến trúc Test Repo**: Tuân thủ mẫu test xUnit có sẵn (`[Collection("DatabaseCollection")]`, `IAsyncLifetime`, `ResetDatabaseAsync` trong `InitializeAsync`).
2. **Ưu tiên Authz & Bảo vệ Dữ liệu (Các test ⭐)**: Khóa chặt các nhánh kiểm tra phân quyền (chống người ngoài đọc DM reaction, chống sửa/xóa tin nhắn người khác) và đảm bảo nội dung tin nhắn đã thu hồi bị xóa sạch khỏi các truy vấn SQL SELECT.
3. **Thành phần mới**: Tạo mới `Integration/ReactionRepositoryTests.cs` (6 tests), `Integration/MessageRepositoryTests.cs` (10 tests), và mở rộng `Integration/ChatHubTests.cs` (3 tests).

---

## 🏗️ Phân bổ Công việc Chi tiết

### Phase 1: Integration/ReactionRepositoryTests.cs (6 tests)
- `ToggleReaction_TwiceBySameUser_AddsThenRemoves`
- `ToggleReaction_MultipleUsers_CountsAggregate`
- `GetReactionsForMessages_ReturnsCount_AndMineReactedTrueForSelf`
- ⭐ `GetReactionsForMessages_DmMessage_HiddenFromNonParticipant`
- `GetReactionsForMessages_RoomMessage_VisibleToAnyAuthenticatedUser`
- `LookupConversation_ReturnsRoomId_OrPrivateChatId`

### Phase 2: Integration/MessageRepositoryTests.cs (10 tests)
- ⭐ `EditMessage_OwnMessage_UpdatesContent_AndSetsEditedAt`
- ⭐ `EditMessage_OtherUsersMessage_ReturnsZeroAffected`
- `EditMessage_AlreadyDeleted_ReturnsZeroAffected`
- ⭐ `SoftDeleteMessage_OwnMessage_SetsDeletedAt`
- ⭐ `SoftDeleteMessage_OtherUsersMessage_ReturnsZeroAffected`
- ⭐ `GetMessagesByRoom_DeletedMessage_BlanksContent`
- `GetMessagesByRoom_ReplySnapshot_PopulatesSenderAndTruncatedContent`
- `GetMessagesByRoom_ReplyToDeleted_SnapshotContentNull`
- `GetMessagesByRoom_BeforeIdAndAfterId_KeysetPaginationWorks`
- `SaveNewMessage_SetsSenderId`

### Phase 3: Integration/ChatHubTests.cs Extension (3 tests)
- ⭐ `ToggleReaction_NonParticipantDm_ThrowsHubException`
- ⭐ `EditMessage_NonOwner_ThrowsHubException`
- ⭐ `DeleteMessage_NonOwner_ThrowsHubException`

### Phase 4: Verification & Documentation
- **Chạy Test Suite**: `dotnet test` (toàn bộ 32 tests xanh).
- **Checklist & Docs**: Cập nhật `TODOS.md` và `context.md`.
- **Git Commit**: `test: phủ test cho reactions, edit/delete, reply, keyset (4.2→5.3)`.
