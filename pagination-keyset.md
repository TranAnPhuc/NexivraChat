# Kế hoạch triển khai GĐ4.2 — Pagination "tải tin cũ" (keyset-based)

Tài liệu này chi tiết hóa kế hoạch phát triển cho giai đoạn GĐ4.2 của dự án NexivraChat: Chuyển đổi cơ chế phân trang lịch sử tin nhắn từ offset-based sang keyset-based (sử dụng `beforeId`), đồng thời tối ưu hóa trải nghiệm cuộn ở frontend (giữ nguyên vị trí cuộn khi tải tin cũ, tự động tải qua Intersection Observer và nút fallback).

## 1. Overview (Tổng quan)
- **Mục tiêu**: Thay thế cơ chế phân trang `limit=50&offset=0` hiện tại. Phân trang offset dễ bị lệch vị trí (duplicate hoặc bỏ sót tin) khi có tin mới được gửi liên tục. Keyset pagination sử dụng ID của tin nhắn cũ nhất làm mốc (`beforeId`) giúp đảm bảo tính nhất quán của dữ liệu.
- **Yêu cầu cốt lõi**:
  - Tải tin cũ khi cuộn lên đầu trang (hoặc bấm nút).
  - Tránh hiện tượng giật màn hình / nhảy vị trí cuộn (Scroll Preservation).
  - Loại bỏ các tin nhắn trùng lặp.
  - Bỏ qua các ID tin nhắn AI tạm thời (giá trị âm) khi xác định ID tin nhắn cũ nhất.

## 2. Project Type (Loại dự án)
- **WEB** (Full-Stack Refactor: C# Backend API & React TS Frontend)

## 3. Success Criteria (Tiêu chí thành công)
1. Cuộn lên đầu danh sách chat tự động tải thêm tin nhắn cũ hơn.
2. Vị trí scroll được giữ nguyên khi prepending tin nhắn cũ (không nhảy scroll xuống đáy hoặc giật màn hình).
3. Không xảy ra trùng lặp tin nhắn trên UI.
4. `dotnet build` và `dotnet test` xanh hoàn toàn.
5. `npm run build` ở thư mục frontend không có lỗi type-checking.
6. Cập nhật `context.md` và `TODOS.md`.
7. Thực hiện commit theo convention: `feat(pagination): phân trang tải tin cũ bằng keyset theo id` bằng tiếng Việt.

## 4. Tech Stack (Công nghệ áp dụng)
- **Backend**: .NET 8, Web API, Dapper async.
  - *Lý do*: Tuân thủ quy ước repo, truy vấn tối ưu, liệt kê cột rõ ràng, không sử dụng `SELECT *`.
- **Frontend**: React 19, TypeScript, Ant Design, CSS custom scrollbar.
  - *Lý do*: Sử dụng API `IntersectionObserver` để kích hoạt tự động tải và `flushSync` từ `react-dom` để đồng bộ cập nhật DOM trước khi đo chiều cao nhằm bù trừ `scrollTop`.

## 5. File Structure (Các file sửa đổi)
- `backend/NexivraChatBackend/Repositories/MessageRepository.cs` (Sửa truy vấn SQL Dapper, đổi chữ ký phương thức)
- `backend/NexivraChatBackend/Controllers/RoomsController.cs` (Cập nhật endpoint phòng chat)
- `backend/NexivraChatBackend/Controllers/UsersController.cs` (Cập nhật endpoint chat 1-1)
- `backend/NexivraChatBackend/Hubs/ChatHub.cs` (Cập nhật cuộc gọi GetMessagesByRoom cho AI context)
- `frontend/nexivra-chat-frontend/src/views/ChatView.tsx` (Thêm state, ref, Intersection Observer, logic bù scroll và nút bấm)
- `context.md` (Cập nhật tài liệu bối cảnh hệ thống)
- `TODOS.md` (Đánh dấu hoàn thành)

---

## 6. Task Breakdown (Chi tiết công việc)

### P0: Foundation & Backend Services

#### Task 1: Cập nhật MessageRepository.cs
- **Agent**: `backend-specialist`
- **Skills**: `database-design`, `clean-code`
- **Dependencies**: Không
- **INPUT**: `backend/NexivraChatBackend/Repositories/MessageRepository.cs`
- **OUTPUT**:
  - Sửa chữ ký `GetMessagesByRoom(int roomId, int limit = 50, int? beforeId = null)`.
  - Thay thế tham số `offset` bằng `beforeId` dạng nullable.
  - Truy vấn SQL (Dapper async):
    ```sql
    SELECT id, room_id, private_chat_id, sender_name, content, created_at, is_ai 
    FROM messages 
    WHERE room_id = @roomId AND (@beforeId IS NULL OR id < @beforeId) 
    ORDER BY id DESC LIMIT @limit;
    ```
    Sau đó thực hiện `.Reverse()` kết quả trả về trước khi `return`.
  - Thực hiện điều chỉnh tương tự cho `GetMessagesByPrivateChat(int privateChatId, int limit = 50, int? beforeId = null)`.
- **VERIFY**:
  - `dotnet build` chạy thành công, không phát sinh lỗi biên dịch.

#### Task 2: Cập nhật gọi phương thức tại ChatHub.cs
- **Agent**: `backend-specialist`
- **Skills**: `clean-code`
- **Dependencies**: Task 1
- **INPUT**: `backend/NexivraChatBackend/Hubs/ChatHub.cs` dòng 127
- **OUTPUT**:
  - Đổi cuộc gọi `GetMessagesByRoom(roomId, 10, 0)` thành `GetMessagesByRoom(roomId, 10, null)` để lấy 10 tin nhắn mới nhất làm ngữ cảnh cho AI.
- **VERIFY**:
  - `dotnet build` không lỗi.

#### Task 3: Expose beforeId tại Controllers
- **Agent**: `backend-specialist`
- **Skills**: `api-patterns`
- **Dependencies**: Task 1
- **INPUT**: 
  - `backend/NexivraChatBackend/Controllers/RoomsController.cs` (dòng 49)
  - `backend/NexivraChatBackend/Controllers/UsersController.cs` (dòng 93)
- **OUTPUT**:
  - Sửa tham số endpoint:
    - Phòng chat: `[HttpGet("{id}/messages")] public async Task<IActionResult> GetRoomMessages(int id, [FromQuery] int limit = 50, [FromQuery] int? beforeId = null)`
    - DM: `[HttpGet("private-chat/{id}/messages")] public async Task<IActionResult> GetPrivateChatMessages(int id, [FromQuery] int limit = 50, [FromQuery] int? beforeId = null)`
  - Truyền `beforeId` vào phương thức Repository tương ứng.
- **VERIFY**:
  - `dotnet build` thành công.
  - Có thể test gọi thử API qua các client HTTP dạng `/api/rooms/{id}/messages?limit=50&beforeId=100` hoạt động bình thường.

---

### P1: Frontend Integration & Scroll Controls

#### Task 4: Khởi tạo State & Ref phân trang tại ChatView.tsx
- **Agent**: `frontend-specialist`
- **Skills**: `nextjs-react-expert`
- **Dependencies**: Task 3
- **INPUT**: `frontend/nexivra-chat-frontend/src/views/ChatView.tsx`
- **OUTPUT**:
  - Khai báo các state mới:
    - `hasMore` (boolean, mặc định `true`)
    - `loadingOlder` (boolean, mặc định `false`)
  - Khai báo các ref mới:
    - `messagesContainerRef` (gán vào thẻ div bao quanh danh sách tin nhắn chat)
    - `isPrependingRef` (boolean ref, mặc định `false`, để kiểm soát chặn auto-scroll xuống đáy)
  - Thêm computed value `oldestMessageId`:
    ```typescript
    const oldestMessageId = (() => {
      const positiveIds = messages.filter(m => m.id > 0).map(m => m.id);
      return positiveIds.length > 0 ? Math.min(...positiveIds) : null;
    })();
    ```
- **VERIFY**:
  - Code TypeScript biên dịch thành công.

#### Task 5: Cập nhật hàm fetchMessageHistory ban đầu & Reset State
- **Agent**: `frontend-specialist`
- **Skills**: `nextjs-react-expert`
- **Dependencies**: Task 4
- **INPUT**: `frontend/nexivra-chat-frontend/src/views/ChatView.tsx`
- **OUTPUT**:
  - Điều chỉnh `fetchMessageHistory` và `fetchPrivateMessageHistory` để reset pagination state khi đổi phòng:
    - Reset `setHasMore(true)` và `setLoadingOlder(false)`.
    - Gọi API bỏ `offset=0` thay bằng `beforeId=` (hoặc để trống).
    - Dựa vào kết quả trả về `data.length`, nếu `< 50` thì đặt `setHasMore(false)`.
- **VERIFY**:
  - Chuyển đổi giữa các phòng/DM tải đúng 50 tin nhắn đầu tiên.

#### Task 6: Implement logic tải tin nhắn cũ & Scroll Preservation
- **Agent**: `frontend-specialist`
- **Skills**: `nextjs-react-expert`, `clean-code`
- **Dependencies**: Task 5
- **INPUT**: `frontend/nexivra-chat-frontend/src/views/ChatView.tsx`
- **OUTPUT**:
  - Xây dựng hàm `loadOlderMessages`:
    - Nếu `loadingOlder` hoặc `!hasMore` hoặc `!oldestMessageId` thì ngắt sớm.
    - Set `loadingOlder(true)` và `isPrependingRef.current = true`.
    - Đo vị trí hiện tại:
      ```typescript
      const container = messagesContainerRef.current;
      const prevScrollHeight = container ? container.scrollHeight : 0;
      const prevScrollTop = container ? container.scrollTop : 0;
      ```
    - Gửi request đến API (`/rooms/${activeRoomId}/messages` hoặc `/users/private-chat/${activePrivateChatId}/messages`) truyền `limit=50&beforeId=${oldestMessageId}`.
    - Xử lý gộp tin nhắn mới lấy được vào đầu danh sách (prepend) và lọc trùng lặp ID thông qua `flushSync`:
      ```typescript
      import { flushSync } from 'react-dom';
      // ...
      flushSync(() => {
        setMessages(prev => {
          const merged = [...response.data, ...prev];
          const unique: Message[] = [];
          const seen = new Set<number>();
          for (const m of merged) {
            if (!seen.has(m.id)) {
              seen.add(m.id);
              unique.push(m);
            }
          }
          return unique;
        });
      });
      ```
    - Thực hiện bù trừ vị trí scroll để giữ nguyên màn hình:
      ```typescript
      if (container) {
        const newScrollHeight = container.scrollHeight;
        container.scrollTop = prevScrollTop + (newScrollHeight - prevScrollHeight);
      }
      ```
    - Reset `isPrependingRef.current = false`, `setLoadingOlder(false)`.
    - Nếu `response.data.length < 50`, set `setHasMore(false)`.
  - Cập nhật `useEffect` auto-scroll hiện tại: chỉ scroll xuống dưới đáy khi `isPrependingRef.current` là `false`.
- **VERIFY**:
  - Bấm tải tin cũ hơn, tin nhắn mới prepended lên phía trên mà vị trí góc nhìn của màn hình chat không bị giật hay dịch chuyển.

#### Task 7: Tích hợp Sentinel & IntersectionObserver + Fallback Button
- **Agent**: `frontend-specialist`
- **Skills**: `nextjs-react-expert`, `frontend-design`
- **Dependencies**: Task 6
- **INPUT**: `frontend/nexivra-chat-frontend/src/views/ChatView.tsx`
- **OUTPUT**:
  - Gán `ref={messagesContainerRef}` cho thẻ div bọc danh sách tin nhắn.
  - Đặt Sentinel Div và nút fallback thủ công ở đầu danh sách tin nhắn chat:
    ```typescript
    {hasMore && messages.length > 0 && (
      <div ref={sentinelRef} style={{ height: '5px', margin: '-5px 0' }} />
    )}
    {hasMore && messages.length > 0 && (
      <div style={{ display: 'flex', justifyContent: 'center', padding: '10px 0' }}>
        <Button type="link" loading={loadingOlder} onClick={loadOlderMessages}>
          Tải tin nhắn cũ hơn
        </Button>
      </div>
    )}
    {!hasMore && messages.length > 0 && (
      <div style={{ textAlign: 'center', color: 'var(--text-muted)', padding: '10px 0', fontSize: '12px' }}>
        Đầu hội thoại
      </div>
    )}
    ```
  - Xây dựng `useEffect` lắng nghe IntersectionObserver trên `sentinelRef.current`:
    - Đặt root là `messagesContainerRef.current`.
    - Khi sentinel hiển thị trên màn hình (`isIntersecting` = true), tự động gọi `loadOlderMessages()`.
- **VERIFY**:
  - Cuộn lên đầu tự động kích hoạt tải tin nhắn cũ. Khi cuộn tới kịch trần (Đầu hội thoại), Sentinel biến mất, hiện "Đầu hội thoại".

---

### P2: Verification & Polish

#### Task 8: Chạy Test Suite & Build Hệ Thống
- **Agent**: `qa-automation-engineer` / `devops-engineer`
- **Skills**: `testing-patterns`, `verify-changes`
- **Dependencies**: Tất cả các task trước
- **INPUT**: Toàn bộ codebase
- **OUTPUT**:
  - Chạy `dotnet test` thành công và tất cả integration tests cũ/mới đều xanh.
  - Chạy `npm run build` thành công ở folder `frontend/nexivra-chat-frontend` không báo lỗi kiểu dữ liệu.
- **VERIFY**:
  - Kết quả terminal trả về exit code 0 cho cả dotnet test và npm run build.

#### Task 9: Cập nhật tài liệu Context & Todos
- **Agent**: `documentation-writer`
- **Skills**: `plan-writing`
- **Dependencies**: Task 8
- **INPUT**: `context.md`, `TODOS.md`
- **OUTPUT**:
  - Đánh dấu hoàn thành mục GĐ4.2 ở `TODOS.md`.
  - Cập nhật mô tả phân trang keyset vào `context.md` (phần lịch sử tối ưu).
  - Viết commit với nội dung: `feat(pagination): phân trang tải tin cũ bằng keyset theo id` và push code lên repository.
- **VERIFY**:
  - Files `context.md` và `TODOS.md` hiển thị đúng trạng thái hoàn tất.

---

## Phase X: Final Verification Checklist (Quy trình xác nhận cuối cùng)

Bạn cần thực thi đầy đủ các bước kiểm thử sau đây trước khi đóng giai đoạn:

### 1. Build & Test tự động
- [ ] Chạy lệnh `dotnet build` thành công.
- [ ] Chạy lệnh `dotnet test` trong backend thành công.
- [ ] Chạy lệnh `npm run build` trong frontend không có lỗi type-checking.

### 2. Manual Testing (Kiểm thử thực tế trên UI)
- [ ] Tạo hội thoại có > 50 tin nhắn.
- [ ] Mở hội thoại, chỉ hiển thị đúng 50 tin nhắn mới nhất và nút loader/sentinel.
- [ ] Cuộn chuột lên đầu trang chat -> tin cũ tự động tải, thanh cuộn giãn nở nhưng màn hình hiển thị đứng yên tại chỗ (không bị trượt xuống đáy).
- [ ] Nhấp thử nút "Tải tin nhắn cũ hơn" nếu cuộn tự động chưa kịp kích hoạt -> hoạt động ổn định.
- [ ] Gửi thử tin nhắn từ bot AI `@copilot`, stream hoạt động bình thường, ID âm tạm thời được xử lý chính xác và không làm gián đoạn việc tính toán `oldestMessageId`.
- [ ] Cuộn tải hết toàn bộ tin cũ cho đến khi chạm "Đầu hội thoại", nút tải và loader biến mất hoàn toàn.

### 3. Rule Compliance Checks
- [ ] Không có màu tím (Purple Ban) được sử dụng trên UI. Chỉ sử dụng màu chủ đạo là teal `#0D9488`.
- [ ] Không sử dụng `SELECT *` trong SQL query. Các cột trong Repository được liệt kê tường minh.
- [ ] Toàn bộ query sử dụng Dapper là async (`QueryAsync`, `Reverse()`).

## ✅ PHASE X COMPLETE
- Lint: [ ] Pass
- Security: [ ] No critical issues
- Build: [ ] Success
- Date: [Chưa thực thi]
