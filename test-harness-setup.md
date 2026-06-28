# Plan: Test Harness Setup for Unread Badges (Phase GĐ4.1)

## 1. Overview
Mục tiêu của giai đoạn **GĐ4.1 — Test harness đóng sổ unread-badges** là xây dựng cơ sở hạ tầng tích hợp (Integration Test Harness) cho dự án NexivraChat sử dụng cơ sở dữ liệu Postgres thật chạy trong Docker container thông qua **Testcontainers.PostgreSql** và reset dữ liệu giữa các test case bằng **Respawn**.
Điều này đảm bảo các logic nghiệp vụ quan trọng liên quan đến đếm tin chưa đọc (`ConversationReadRepository`) và phân quyền tham gia chat riêng tư (`ChatHub.MarkRead`) được kiểm thử đầy đủ, độc lập và đáng tin cậy trên một hệ quản trị cơ sở dữ liệu thật thay vì giả lập (mocking) hoặc dùng in-memory database.

## 2. Project Type
**BACKEND / TESTING**

## 3. Success Criteria
- [ ] Dự án test `NexivraChatBackend.Tests` chạy thành công bằng lệnh `dotnet test` và toàn bộ các test cases (bao gồm unit tests có sẵn và integration tests mới) đều đạt (Green).
- [ ] Tích hợp thành công **Testcontainers** để tự động kéo và khởi tạo container Postgres (`postgres:15-alpine`) khi bắt đầu chạy test suite.
- [ ] Tích hợp thành công **Respawn** để dọn dẹp (clean) tất cả các bảng dữ liệu trong Postgres giữa các test case nhằm bảo đảm tính độc lập dữ liệu.
- [ ] Đạt độ phủ kiểm thử (test coverage) tốt cho các phương thức:
  - `ConversationReadRepository.GetUnreadCounts` (với logic JOIN/COALESCE khác nhau giữa Room và DM).
  - `ConversationReadRepository.MarkRoomRead` và `MarkPrivateChatRead` (logic UPSERT `ON CONFLICT` và kiểm tra chống lùi mốc thời gian/tin nhắn bằng `GREATEST`).
  - Kiểm tra tính toàn vẹn dữ liệu từ partial unique indexes (`uq_reads_room` và `uq_reads_dm`).
  - `ChatHub.MarkRead` (kiểm tra phân quyền tham gia chat riêng tư cho DM).

## 4. Tech Stack
- **.NET 8 SDK**: Nền tảng lập trình chính của backend.
- **xUnit**: Testing framework chuẩn của dự án.
- **Microsoft.AspNetCore.Mvc.Testing**: Cung cấp `WebApplicationFactory` để giả lập host API phục vụ E2E Integration Test cho SignalR Hub.
- **Microsoft.AspNetCore.SignalR.Client**: Thư viện SignalR Client để tạo kết nối thực tới `ChatHub`.
- **Testcontainers.PostgreSql**: Thư viện quản lý vòng đời Docker container chạy Postgres tự động từ code C#.
- **Npgsql**: Driver kết nối Postgres.
- **Dapper**: ORM siêu nhẹ dùng cho các câu lệnh SQL trong Repository.
- **Respawn**: Thư viện dọn dẹp dữ liệu database nhanh chóng trước/sau mỗi lượt test mà không cần recreate schema.

## 5. File Structure

### Các file cần chỉnh sửa (Modify):
- `backend/NexivraChatBackend/Program.cs` - Expose class `Program` cho dự án test bằng cách thêm lớp partial.
- `backend/NexivraChatBackend.Tests/NexivraChatBackend.Tests.csproj` - Thêm các gói NuGet dependencies.
- `context.md` - Cập nhật thông tin hạ tầng kiểm thử và kết quả hoàn thành GĐ4.1.
- `TODOS.md` - Đánh dấu hoàn thành hạng mục GĐ4.1.

### Các file cần tạo mới (Create):
- `backend/NexivraChatBackend.Tests/Fixtures/DatabaseFixture.cs` - Quản lý container Postgres và Respawn.
- `backend/NexivraChatBackend.Tests/Fixtures/DatabaseCollection.cs` - Định nghĩa xUnit Collection để chia sẻ Fixture giữa các Class Test.
- `backend/NexivraChatBackend.Tests/Integration/ConversationReadRepositoryTests.cs` - Test repository logic.
- `backend/NexivraChatBackend.Tests/Integration/ChatHubTests.cs` - Test phân quyền SignalR Hub.

---

## 6. Task Breakdown

### Task 1: NuGet Packages Setup in Test Project
- **Agent:** `devops-engineer` / `test-engineer`
- **Skills:** `testing-patterns`
- **Priority:** P0
- **Dependencies:** None
- **INPUT:** `backend/NexivraChatBackend.Tests/NexivraChatBackend.Tests.csproj`
- **OUTPUT:** `NexivraChatBackend.Tests.csproj` được tích hợp thêm các gói:
  - `Testcontainers.PostgreSql`
  - `Respawn`
  - `Microsoft.AspNetCore.Mvc.Testing`
  - `Microsoft.AspNetCore.SignalR.Client`
- **VERIFY:** Chạy lệnh `dotnet restore backend/NexivraChatBackend.Tests/NexivraChatBackend.Tests.csproj` và `dotnet build backend/NexivraChatBackend.Tests/NexivraChatBackend.Tests.csproj` thành công không có lỗi.

### Task 2: Expose Program Class in NexivraChatBackend
- **Agent:** `backend-specialist`
- **Skills:** `clean-code`
- **Priority:** P0
- **Dependencies:** None
- **INPUT:** `backend/NexivraChatBackend/Program.cs`
- **OUTPUT:** Thêm dòng khai báo `public partial class Program { }` ở cuối file `Program.cs` nhằm cho phép `WebApplicationFactory<Program>` trong dự án Test có thể tham chiếu tới.
- **VERIFY:** Chạy lệnh `dotnet build backend/NexivraChatBackend/NexivraChatBackend.csproj` thành công.

### Task 3: Database Fixture Setup using Testcontainers and Respawn
- **Agent:** `test-engineer` / `database-architect`
- **Skills:** `testing-patterns`, `database-design`
- **Priority:** P1
- **Dependencies:** Task 1, Task 2
- **INPUT:** Tạo thư mục `backend/NexivraChatBackend.Tests/Fixtures/`
- **OUTPUT:**
  - `DatabaseFixture.cs`:
    - Khởi tạo `PostgreSqlContainer` sử dụng image `postgres:15-alpine` (đồng bộ với docker-compose.yml).
    - Triển khai `IAsyncLifetime` để bắt đầu container trong `InitializeAsync` và tắt container trong `DisposeAsync`.
    - Gọi `DbInitializer.Initialize` để tự động tạo toàn bộ cấu trúc bảng và index.
    - Cấu hình và tạo instance `Respawner` trỏ tới DB Postgres của container (sử dụng `DbAdapter.Postgres`). Expose phương thức `ResetDatabaseAsync` để dọn dẹp các bảng trước/sau mỗi test case.
  - `DatabaseCollection.cs`:
    - Tạo lớp `DatabaseCollection` kế thừa `ICollectionFixture<DatabaseFixture>` và đánh dấu attribute `[CollectionDefinition("DatabaseCollection")]` để chia sẻ database container duy nhất giữa các file test.
- **VERIFY:** Biên dịch dự án test thành công: `dotnet build backend/NexivraChatBackend.Tests/NexivraChatBackend.Tests.csproj`.

### Task 4: Integration Tests for ConversationReadRepository
- **Agent:** `test-engineer`
- **Skills:** `testing-patterns`, `database-design`
- **Priority:** P1
- **Dependencies:** Task 3
- **INPUT:** Tạo file `backend/NexivraChatBackend.Tests/Integration/ConversationReadRepositoryTests.cs`
- **OUTPUT:** Triển khai các test cases sử dụng shared `DatabaseFixture`:
  - **`GetUnreadCounts`**:
    - Thêm dữ liệu giả lập (seed) gồm 2 phòng (General, AI Lounge) và 1 DM chat giữa UserA và UserB. Gửi tin nhắn mới.
    - Trường hợp phòng (Room): Nếu User chưa từng mở phòng (chưa có row trong `conversation_reads` cho room), unread count phải trả về 0 (INNER JOIN).
    - Trường hợp DM (PrivateChat): Nếu User chưa từng mở DM, unread count vẫn phải đếm đầy đủ số tin nhắn nhận được (LEFT JOIN + COALESCE).
  - **`MarkRoomRead` / `MarkPrivateChatRead`**:
    - Gửi tin nhắn có ID lớn (ví dụ: ID = 10). Gọi `MarkRoomRead` với `lastReadMessageId = 10`. Kiểm tra bảng `conversation_reads` lưu đúng mốc 10.
    - Gọi tiếp `MarkRoomRead` với `lastReadMessageId = 5` (đến trễ hoặc do race condition). Kiểm tra `last_read_message_id` trong DB vẫn giữ nguyên giá trị 10 nhờ hàm `GREATEST` trong câu lệnh UPSERT.
  - **Index Integrity Check**:
    - Thực hiện ghi đè dữ liệu trực tiếp hoặc test hành vi UPSERT để chứng minh 2 partial unique index (`uq_reads_room` và `uq_reads_dm`) hoạt động tốt, ngăn chặn việc tạo trùng dòng read cho cùng 1 user trên cùng phòng/DM.
- **VERIFY:** Chạy lệnh:
  `dotnet test backend/NexivraChatBackend.Tests/NexivraChatBackend.Tests.csproj --filter "FullyQualifiedName~ConversationReadRepositoryTests"`
  và xác nhận các test case chạy qua thành công (Green).

### Task 5: Integration Tests for ChatHub MarkRead Access Control
- **Agent:** `test-engineer`
- **Skills:** `testing-patterns`, `api-patterns`
- **Priority:** P1
- **Dependencies:** Task 3
- **INPUT:** Tạo file `backend/NexivraChatBackend.Tests/Integration/ChatHubTests.cs`
- **OUTPUT:** Triển khai tích hợp SignalR Hub:
  - Viết `CustomWebApplicationFactory : WebApplicationFactory<Program>` để tự động override chuỗi kết nối DefaultConnection trong configuration trỏ sang IP/Port của Postgres Testcontainer.
  - Test case:
    - Tạo User A, User B và User C trong DB. Tạo cuộc hội thoại riêng tư (DM) giữa User A và User B.
    - Gửi tin nhắn từ User A tới User B.
    - Tạo JWT Token cho User C (người ngoài cuộc). Kết nối User C tới SignalR `/chatHub` sử dụng token đó.
    - Gọi phương thức Hub `MarkRead(roomId: null, privateChatId: dmId, lastReadMessageId: msgId)`.
    - Kiểm tra xem Hub có ném ra lỗi `HubException` với thông báo `"Bạn không có quyền với hội thoại này."` hay không.
    - Tạo kết nối của User B (thành viên hội thoại). Gọi `MarkRead` tương tự và đảm bảo không lỗi, đồng thời kiểm tra database ghi nhận đúng trạng thái đọc của User B.
- **VERIFY:** Chạy lệnh:
  `dotnet test backend/NexivraChatBackend.Tests/NexivraChatBackend.Tests.csproj --filter "FullyQualifiedName~ChatHubTests"`
  và xác nhận các test case SignalR Hub chạy qua thành công (Green).

### Task 6: Documentation and Repository Housekeeping
- **Agent:** `documentation-writer`
- **Skills:** `memory-system`
- **Priority:** P2
- **Dependencies:** Task 4, Task 5
- **INPUT:** `context.md`, `TODOS.md`
- **OUTPUT:**
  - Cập nhật `context.md` để bổ sung phần giới thiệu hạ tầng integration test bằng Testcontainers + Respawn.
  - Cập nhật `TODOS.md` tick hoàn thành mục `- [ ] GĐ4.1 Test harness đóng sổ unread-badges`.
- **VERIFY:** Chạy toàn bộ test suite dự án bằng:
  `dotnet test backend/NexivraChatBackend.Tests/NexivraChatBackend.Tests.csproj`
  Tất cả test cases (> 13-14 tests bao gồm unit & integration) đều xanh lá.

---

## 7. Phase X: Final Verification Checklist
Sau khi hoàn thành tất cả các tasks lập trình, thực hiện chạy các kiểm tra sau:

- [ ] Dự án Backend compile thành công: `dotnet build backend/NexivraChatBackend/NexivraChatBackend.csproj`.
- [ ] Dự án Test compile thành công: `dotnet build backend/NexivraChatBackend.Tests/NexivraChatBackend.Tests.csproj`.
- [ ] Chạy toàn bộ test suite thành công: `dotnet test backend/NexivraChatBackend.Tests/NexivraChatBackend.Tests.csproj`.
- [ ] Không có file mã nguồn C# (.cs) nào được tạo/chỉnh sửa trong quá trình chuẩn bị PLAN.
- [ ] File plan này `test-harness-setup.md` tồn tại trong thư mục gốc dự án.
- [ ] Kiểm tra tuân thủ nguyên tắc thiết kế:
  - [ ] Không sử dụng Entity Framework Core (chỉ dùng Dapper).
  - [ ] Các câu lệnh SQL trong test helper (nếu có) không sử dụng `SELECT *` và gọi tên cột rõ ràng.
  - [ ] Không sử dụng màu tím trong bất kỳ tài liệu hay giao diện (Purple Ban).
