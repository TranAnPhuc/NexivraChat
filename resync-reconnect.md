# Plan: GĐ4.3 Resync after Reconnect (resync-reconnect.md)

> **Mục tiêu**: Người dùng khi mất kết nối mạng và kết nối lại (SignalR Reconnected) sẽ không bị mất các tin nhắn được gửi trong thời gian mất mạng. Client sẽ tự động kéo các tin nhắn bị nhỡ kể từ tin nhắn nhận được cuối cùng (`lastReceivedMessageId`) và append + dedupe vào hội thoại đang mở một cách trơn tru, đồng thời cập nhật số tin chưa đọc.

---

## 1. Overview
- **Vấn đề hiện tại**: SignalR có cơ chế `.withAutomaticReconnect()` tự động kết nối lại khi mất mạng. Tuy nhiên, khi kết nối thành công, hệ thống chỉ refetch lại số tin chưa đọc (`unread-counts`) chứ chưa tự động kéo các tin nhắn mới được gửi trong khoảng thời gian mất mạng cho hội thoại đang active.
- **Giải pháp**:
  - **Backend**: Nâng cấp `MessageRepository` hỗ trợ tham số `afterId` (WHERE id > @afterId ORDER BY id ASC LIMIT @limit) để truy vấn các tin nhắn mới gửi sau mốc ID nhất định. Expose tham số này qua API Query Params `?afterId=` tại endpoint messages của Room và Private Chat.
  - **Frontend**: Track ID tin nhắn thật lớn nhất của hội thoại hiện tại (`lastReceivedMessageId`). Khi sự kiện `onreconnected` kích hoạt, thực hiện gọi API fetch tin nhắn với `afterId=lastReceivedMessageId` cho hội thoại đang active, sau đó thực hiện gộp tin và loại bỏ trùng lặp (deduplicate) âm thầm trước khi cập nhật state.

---

## 2. Project Type
- **Type**: FULL STACK / REFACTOR

---

## 3. Success Criteria
- [ ] Khi ngắt kết nối mạng và có người khác gửi tin nhắn mới, khi có mạng lại, client sẽ tự động nhận diện và hiển thị đầy đủ tin nhắn bị bỏ lỡ mà không cần tải lại trang.
- [ ] Không xuất hiện tin nhắn bị trùng lặp trên giao diện (silent deduplication hoạt động chính xác).
- [ ] Logic refetch số tin chưa đọc (`unread-counts`) vẫn hoạt động bình thường trên reconnect.
- [ ] `dotnet build` và `dotnet test` chạy thành công (xanh).
- [ ] `npm run build` trên frontend chạy thành công không có lỗi TypeScript.
- [ ] Cập nhật `context.md` và `TODOS.md` chính xác.

---

## 4. Tech Stack
- **Backend**: .NET 8, Dapper (Async queries, explicit column projection), Web API Controllers.
- **Frontend**: React 19 (TypeScript, Hooks: `useRef`, `useEffect`), SignalR Client, Axios, `flushSync`.

---

## 5. File Structure
Các file cần được chỉnh sửa:
- `backend/NexivraChatBackend/Repositories/MessageRepository.cs` (Thêm query `afterId`)
- `backend/NexivraChatBackend/Controllers/RoomsController.cs` (Expose `afterId` query param)
- `backend/NexivraChatBackend/Controllers/UsersController.cs` (Expose `afterId` query param)
- `frontend/nexivra-chat-frontend/src/views/ChatView.tsx` (Track `lastReceivedMessageId` qua Ref và thực hiện resync trong `onreconnected`)
- `context.md` (Cập nhật lịch sử và thông tin tính năng)
- `TODOS.md` (Đánh dấu hoàn thành GĐ4.3)

---

## 6. Task Breakdown

### Phase 1: Backend Refactor (P0)

#### TSK-001: Modify MessageRepository.cs
- **Agent**: `backend-specialist`
- **Skill**: `clean-code`, `database-design`
- **Priority**: High (Blocker for API controllers)
- **Input**: `backend/NexivraChatBackend/Repositories/MessageRepository.cs`
- **Output**: 
  - Thêm tham số `int? afterId = null` vào chữ ký của 2 phương thức:
    - `GetMessagesByRoom(int roomId, int limit = 50, int? beforeId = null, int? afterId = null)`
    - `GetMessagesByPrivateChat(int privateChatId, int limit = 50, int? beforeId = null, int? afterId = null)`
  - Sử dụng logic điều kiện trong C# để tối ưu hóa câu lệnh SQL:
    - Nếu có `afterId`: sử dụng truy vấn sắp xếp tăng dần `WHERE id > @afterId ORDER BY id ASC LIMIT @limit` (không cần gọi `.Reverse()` vì dữ liệu đã theo thứ tự thời gian tăng dần).
    - Nếu không có `afterId`: giữ nguyên logic cũ (sắp xếp giảm dần `WHERE id < @beforeId ORDER BY id DESC LIMIT @limit` và gọi `.Reverse()`).
  - **Quy tắc**: Tuyệt đối không sử dụng `SELECT *`, liệt kê rõ ràng các cột cần lấy: `id, room_id, private_chat_id, sender_name, content, created_at, is_ai`.
- **Verify**: Chạy biên dịch backend (`dotnet build`) để đảm bảo không lỗi cú pháp.

#### TSK-002: Expose `afterId` in RoomsController.cs & UsersController.cs
- **Agent**: `backend-specialist`
- **Skill**: `api-patterns`, `clean-code`
- **Priority**: High
- **Input**: 
  - `backend/NexivraChatBackend/Controllers/RoomsController.cs`
  - `backend/NexivraChatBackend/Controllers/UsersController.cs`
- **Output**:
  - Tại `GetRoomMessages` (RoomsController), thêm `[FromQuery] int? afterId = null` vào tham số HTTP Get. Truyền giá trị này vào `GetMessagesByRoom`.
  - Tại `GetPrivateChatMessages` (UsersController), thêm `[FromQuery] int? afterId = null` vào tham số HTTP Get. Truyền giá trị này vào `GetMessagesByPrivateChat`.
- **Verify**: Chạy lệnh `dotnet build` thành công.

---

### Phase 2: Frontend Refactor (P1)

#### TSK-003: Dynamically Track lastReceivedMessageId in ChatView.tsx
- **Agent**: `frontend-specialist`
- **Skill**: `nextjs-react-expert`, `clean-code`
- **Priority**: High
- **Input**: `frontend/nexivra-chat-frontend/src/views/ChatView.tsx`
- **Output**:
  - Tạo một Ref để lưu trữ tin nhắn nhận được cuối cùng: `const lastReceivedMessageIdRef = useRef<number | null>(null);`
  - Viết một `useEffect` theo dõi sự thay đổi của state `messages` để cập nhật Ref:
    ```typescript
    useEffect(() => {
      const positiveIds = messages.filter(m => m.id > 0).map(m => m.id);
      lastReceivedMessageIdRef.current = positiveIds.length > 0 ? Math.max(...positiveIds) : null;
    }, [messages]);
    ```
    *(Sử dụng Ref giúp tránh closure bị stale bên trong callback `conn.onreconnected` khi kết nối lại).*
- **Verify**: Sử dụng `npx tsc --noEmit` trong thư mục frontend để kiểm tra lỗi type.

#### TSK-004: Implement Resync & Silent Deduplication in conn.onreconnected
- **Agent**: `frontend-specialist`
- **Skill**: `nextjs-react-expert`, `clean-code`
- **Priority**: High
- **Input**: `frontend/nexivra-chat-frontend/src/views/ChatView.tsx`
- **Output**:
  - Thay đổi callback `conn.onreconnected` trong `useEffect` khởi tạo SignalR:
    - Tiếp tục gọi `fetchUnreadCounts()`.
    - Lấy `lastId = lastReceivedMessageIdRef.current`.
    - Nếu `activeChatTypeRef.current === 'room'` và có `activeRoomIdRef.current`:
      - Rejoin room: `conn.invoke('JoinRoom', activeRoomIdRef.current)`
      - Nếu `lastId` hợp lệ (khác null): Gọi API `GET /api/rooms/{roomId}/messages?limit=50&afterId={lastId}`.
        - Khi có dữ liệu trả về, tiến hành gộp vào state `messages` hiện tại và thực hiện deduplicate âm thầm (silent deduplication) dựa trên `id` tin nhắn.
        - Nếu API lỗi, fallback về `fetchMessageHistory(activeRoomIdRef.current)` để đảm bảo tính nhất quán của dữ liệu.
      - Nếu `lastId` null: Gọi `fetchMessageHistory(activeRoomIdRef.current)`.
    - Làm tương tự đối với Private Chat (`activeChatTypeRef.current === 'private'`).
- **Verify**: Chạy `npm run build` trên frontend không có bất kỳ cảnh báo hoặc lỗi build nào.

---

### Phase 3: Documentation & Verification (P2)

#### TSK-005: Update context.md & TODOS.md
- **Agent**: `documentation-writer`
- **Skill**: `documentation-templates`
- **Priority**: Medium
- **Input**: `context.md`, `TODOS.md`
- **Output**:
  - Đánh dấu hoàn thành mục `GĐ4.3` trong `TODOS.md` (`- [x] GĐ4.3 Resync đầy đủ sau reconnect`).
  - Thêm thông tin mô tả cơ chế resync sau reconnect vào `context.md` (mục SignalR/Realtime).
- **Verify**: Xem lại thay đổi bằng git diff.

---

## Phase X: Final Verification Checklist

Sau khi thực hiện xong tất cả các task trên, thực hiện các bước kiểm thử sau:
1. **Kiểm tra biên dịch**:
   - Chạy `dotnet build` ở thư mục `backend/` -> Thành công không có lỗi.
   - Chạy `npm run build` ở thư mục `frontend/nexivra-chat-frontend/` -> Thành công không có lỗi type.
2. **Kiểm tra tự động (Tests)**:
   - Chạy `dotnet test` ở thư mục `backend/` -> Tất cả test cases màu xanh.
3. **Kiểm tra thủ công (Manual E2E Test)**:
   - Mở 2 trình duyệt/tab khác nhau, kết nối vào cùng phòng chat hoặc DM.
   - Trình duyệt A tắt mạng (ngắt tab mạng trong DevTools/Network -> Offline).
   - Trình duyệt B gửi 3 tin nhắn mới.
   - Trình duyệt A bật lại mạng (Online).
   - Xác nhận: 3 tin nhắn mới xuất hiện trên trình duyệt A một cách trơn tru, không có tin nhắn trùng lặp, không bị nhấp nháy/xóa lịch sử cũ (trừ trường hợp lỗi gọi API mới fallback về fetch full).
4. **Quy chuẩn Code**:
   - [ ] Đã sử dụng Dapper async và liệt kê cột tường minh.
   - [ ] Không có hex code tím/violet (tuân thủ Teal `#0D9488`).
   - [ ] Sử dụng tiếng Việt thân thiện trong các thông báo lỗi và log.
   - [ ] Đã hoàn thành checklist an toàn và type-safe.

## ✅ PHASE X COMPLETE
- Lint: [ ] Pass
- Security: [ ] No critical issues
- Build: [ ] Success
- Date: [Current Date]
