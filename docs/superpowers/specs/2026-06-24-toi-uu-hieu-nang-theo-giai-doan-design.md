# Thiết kế: Tối ưu hiệu năng NexivraChat theo giai đoạn

- **Ngày:** 2026-06-24
- **Phạm vi:** Backend (.NET 8 / SignalR / Dapper / PostgreSQL) và Frontend (React 19 / antd / Vite)
- **Mục tiêu:** Hiệu năng (performance) — giảm block thread, query DB nhanh/ổn định, giảm re-render frontend, bundle gọn hơn.
- **Chiến lược:** Cách C — gom các "quick win" độc lập làm trước, sau đó mới tới các thay đổi cấu trúc lớn. Mỗi giai đoạn = 1 spec + 1 plan + 1 lần thực thi riêng, ship và kiểm chứng độc lập.
- **Baseline:** Toàn bộ code hiện tại (kể cả các file chưa commit: chat 1-1, profile, translation) được coi là baseline ổn định, chạy được.

---

## Bối cảnh & vấn đề đã xác định (grounded)

Các điểm nghẽn hiệu năng tìm thấy khi đọc code thực tế:

### Backend
1. **DB call đồng bộ trong Hub async.** `MessageRepository.SaveNewMessage`, `GetMessagesByRoom`, v.v. dùng `connection.Query` / `ExecuteScalar` (đồng bộ) nhưng được gọi bên trong các method async của `ChatHub` (`SendMessage`, `SendPrivateMessage`). Mỗi tin nhắn block 1 thread của thread pool → nghẽn lớn nhất phía backend khi nhiều người chat đồng thời.
   - `backend/NexivraChatBackend/Repositories/MessageRepository.cs:51`
   - `backend/NexivraChatBackend/Hubs/ChatHub.cs:105`
2. **`new HttpClient()` trực tiếp** trong `AiService` (đăng ký Scoped) → tạo socket mới mỗi scope, nguy cơ socket exhaustion. `TranslationService` tương tự.
   - `backend/NexivraChatBackend/Services/AiService.cs:19`
3. **Thiếu index DB** trên `messages(room_id, created_at)`, `messages(private_chat_id, created_at)`, `messages(sender_name)` → query lịch sử chậm dần khi dữ liệu lớn.
4. **`new Random().Next()`** tạo mỗi tin nhắn AI để sinh temp-ID âm → cấp phát thừa + nguy cơ trùng ID. `backend/NexivraChatBackend/Hubs/ChatHub.cs:132`
5. **`SELECT *`** ở mọi query repository → đọc thừa cột, dễ vỡ khi schema đổi.

### Frontend
6. **`ChatView.tsx` (598 dòng) re-render toàn bộ danh sách tin nhắn mỗi token AI.** Handler `ReceiveAiToken` cập nhật mảng `messages` → cả list `.map()` lại; chưa tách `MessageBubble` + `React.memo`. Nghẽn lớn nhất phía frontend.
   - `frontend/nexivra-chat-frontend/src/views/ChatView.tsx:192`
7. **`scrollIntoView({behavior:'smooth'})` chạy mỗi token** → giật khi AI trả lời dài. `frontend/nexivra-chat-frontend/src/views/ChatView.tsx:288`
8. **Không virtualize** danh sách tin nhắn; **không code-split** (`ProfileView` luôn mount, antd icons import full).

---

## Giai đoạn 1 — Quick wins (sửa nhỏ, độc lập, rủi ro thấp)

Không đụng kiến trúc, chỉ chỉnh trong chỗ. Mỗi sửa tách biệt nhau.

### Backend
- **Index DB** trong `DbInitializer`: thêm các index (tạo `IF NOT EXISTS` để idempotent):
  - `messages(room_id, created_at)`
  - `messages(private_chat_id, created_at)`
  - `messages(sender_name)`
- **Bỏ `SELECT *`** trong `MessageRepository` (và các repo khác cùng pattern): liệt kê cột tường minh khớp với model (`id, room_id, private_chat_id, sender_name, content, created_at, is_ai`).
- **Thay `new Random().Next()`** sinh temp-ID âm bằng nguồn dùng chung không trùng: dùng `System.Threading.Interlocked.Decrement` trên một biến `static long` (bắt đầu từ -1 đi xuống) hoặc `Random.Shared`. Ưu tiên Interlocked vì đảm bảo không trùng trong tiến trình.

### Frontend
- **Sửa auto-scroll** (`ChatView.tsx:288`): chỉ dùng `behavior:'smooth'` khi không đang stream token AI; trong lúc stream dùng `'auto'` hoặc throttle để hết giật. Giữ hành vi "tự cuộn xuống tin mới".

### Kiểm chứng GĐ1
- Backend `dotnet build` pass; chạy app, gửi tin nhắn thường + `@copilot` hoạt động như cũ.
- `EXPLAIN` trên truy vấn lịch sử cho thấy dùng index mới.
- Frontend build pass; quan sát UI cuộn mượt khi AI stream câu dài.

---

## Giai đoạn 2 — Cấu trúc backend (async I/O)

Thay đổi nhiều chữ ký method nên tách riêng khỏi GĐ1.

- **Async hóa Dapper:** chuyển sang `QueryAsync` / `ExecuteScalarAsync` / `ExecuteAsync` trong tất cả repository: `MessageRepository`, `UserRepository`, `RoomRepository`, `PrivateChatRepository`, `ProfileRepository`. Đổi chữ ký method trả `Task<...>`.
- **Cập nhật caller:** `ChatHub` (`SendMessage`, `SendPrivateMessage`, context AI), các Controller (`RoomsController`, `UsersController`, `ProfileController`, `TranslationController`, `AuthController`) dùng `await`.
- **`IHttpClientFactory`:** đăng ký `AddHttpClient` cho `AiService` và `TranslationService` trong `Program.cs`; bỏ `new HttpClient()` trong service, nhận qua DI.

### Kiểm chứng GĐ2
- `dotnet build` pass; unit test `PresenceTrackerTests` vẫn xanh.
- Gửi tin / AI stream / dịch hoạt động đầy đủ.
- Không còn lệnh Dapper đồng bộ (`connection.Query(` / `ExecuteScalar(` không-async) trong repository.

---

## Giai đoạn 3 — Cấu trúc frontend (render + bundle)

- **Tách `MessageBubble`** thành component riêng, bọc `React.memo`; truyền props ổn định và `useCallback` cho các handler (`handleTranslateMessage`, mở profile...). → mỗi token AI chỉ re-render đúng bong bóng đang stream thay vì cả danh sách.
  - `frontend/nexivra-chat-frontend/src/views/ChatView.tsx:460`
- **Virtualize** danh sách tin nhắn bằng `@tanstack/react-virtual` (hoặc tương đương) cho phòng nhiều tin nhắn; giữ auto-scroll xuống tin mới hoạt động.
- **Lazy-load `ProfileView`** bằng `React.lazy` + `Suspense`; import antd icon tường minh (đã là named import — kiểm tra tree-shaking) để bundle gọn.

### Kiểm chứng GĐ3
- React DevTools Profiler: khi AI stream, chỉ 1 `MessageBubble` re-render.
- `npm run build` pass; so sánh kích thước bundle trước/sau (ProfileView tách chunk riêng).
- Toàn bộ chức năng giữ nguyên (chat phòng, chat 1-1, dịch, profile, presence, typing).

---

## Nguyên tắc xuyên suốt
- Mỗi giai đoạn độc lập, không phụ thuộc giai đoạn sau; có thể dừng lại sau bất kỳ GĐ nào mà app vẫn chạy đúng.
- Không thêm tính năng mới — chỉ tối ưu hiệu năng trên hành vi hiện có.
- Không refactor ngoài phạm vi (chỉ chạm phần phục vụ mục tiêu hiệu năng).
- Sau mỗi GĐ: build + chạy thử + kiểm chứng theo tiêu chí của GĐ đó trước khi sang GĐ kế.

## Ngoài phạm vi (YAGNI)
- Caching tầng (Redis), message queue, horizontal scale SignalR (backplane) — chưa cần ở quy mô hiện tại.
- Đổi ORM / bỏ Dapper, đổi UI library.
- Tối ưu thuật toán AI prompt / model — không thuộc hiệu năng hệ thống.
