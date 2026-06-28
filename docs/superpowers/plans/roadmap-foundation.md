# Roadmap Nền Tảng (Hướng 2) — Spec cho Antigravity thực thi

> Tạo qua `/plan-ceo-review` (Claude), 2026-06-28. Mô hình làm việc:
> **Claude lập kế hoạch + review, Antigravity 2.0 viết code.**
> Quyết định scope: **Nền tảng trước** (giữ Hướng 2, xem `context.md`).

## Cách dùng tài liệu này (cho Antigravity)

Mỗi mục `GĐ4.x` là một đơn vị công việc độc lập, có đủ: **Mục tiêu / DB / Backend /
SignalR / Frontend / Acceptance / Edge cases**. Làm **tuần tự theo thứ tự** (có phụ
thuộc: 4.4 trước 4.5). Sau mỗi mục:

1. Build backend (`dotnet build`) + chạy test (`dotnet test`) — phải xanh.
2. Chạy frontend (`npm run build`) — không lỗi type.
3. Cập nhật `context.md` + tick `TODOS.md`.
4. Commit theo convention repo: `feat(<scope>): ...` tiếng Việt.

Khi gặp việc khó (quyết định kiến trúc, bug dai dẳng, review cuối) → quay lại Claude.

Quy ước repo (tuân thủ tuyệt đối):
- DB: **Dapper async all-the-way**, KHÔNG EF Core, KHÔNG `SELECT *` (liệt kê cột).
- `DbInitializer` idempotent (`IF NOT EXISTS` / `ADD COLUMN IF NOT EXISTS`).
- Frontend: React 19 + antd, teal `#0D9488` (KHÔNG dùng tím), tiếng Việt thân thiện.
- Mọi codepath mới: thêm log/metric tối thiểu + xử lý null/empty/lỗi tường minh.

---

## GĐ4 — Hoàn thiện độ tin cậy nền tảng

### GĐ4.1 — Test harness đóng sổ unread-badges  ⬅️ LÀM TRƯỚC

**Mục tiêu:** unread-badges hiện thiếu test DB + hub (xem `TODOS.md`). Chưa có test thì
chưa coi là hoàn tất. Dựng harness integration test trên Postgres thật.

- **Backend (test project `NexivraChatBackend.Tests`):**
  - Thêm NuGet: `Testcontainers.PostgreSql`, `Respawn`, `Microsoft.AspNetCore.SignalR.Client` (nếu test hub end-to-end).
  - Fixture khởi 1 container Postgres, chạy `DbInitializer` để tạo schema, reset bằng Respawn giữa các test.
  - Test `ConversationReadRepository`:
    - `GetUnreadCounts`: phòng chưa mở = 0 (INNER JOIN), DM chưa mở vẫn đếm đủ (LEFT JOIN + COALESCE).
    - `MarkRoomRead` / `MarkPrivateChatRead`: upsert `ON CONFLICT`, `GREATEST` KHÔNG lùi mốc khi `lastReadMessageId` nhỏ hơn mốc cũ.
    - CHECK đúng-1-target + 2 partial unique index hoạt động (insert sai bị chặn).
  - Test hub `MarkRead`: DM verify participant — user ngoài hội thoại gọi `MarkRead(privateChatId)` phải bị từ chối.
- **Acceptance:** `dotnet test` xanh, coverage các nhánh trên; tick mục test trong `TODOS.md`.
- **Edge cases:** lastReadMessageId = 0/null; gọi MarkRead song song (đua upsert); DM mà user không phải participant.

---

### GĐ4.2 — Pagination "tải tin cũ hơn" (keyset, KHÔNG dùng offset)

**Vấn đề hiện tại:** client luôn gọi `limit=50&offset=0` (`ChatView.tsx`), tin quá 50
không với tới. Offset-pagination lệch khi có tin mới chen vào → dùng **keyset theo `id`**.

- **DB:** không đổi schema (đã có index `(room_id, created_at)` / `(private_chat_id, created_at)`).
  Cân nhắc thêm index `(room_id, id)` / `(private_chat_id, id)` nếu chưa có (unread-badges đã thêm — kiểm tra `DbInitializer`).
- **Backend (`MessageRepository`):** thêm tham số `beforeId`:
  ```sql
  -- GetMessagesByRoom(roomId, limit, beforeId?)
  SELECT id, room_id, private_chat_id, sender_name, content, created_at, is_ai
  FROM messages
  WHERE room_id = @roomId AND (@beforeId IS NULL OR id < @beforeId)
  ORDER BY id DESC LIMIT @limit;   -- rồi Reverse() để trả tăng dần
  ```
  Tương tự cho `GetMessagesByPrivateChat`. Giữ method cũ hoặc đổi chữ ký + cập nhật caller.
- **Endpoints:** thêm `?beforeId=` cho `GET /api/rooms/{id}/messages` và `GET /api/users/private-chat/{id}/messages` (bỏ dần `offset`).
- **Frontend (`ChatView.tsx`):**
  - State: `oldestMessageId` (id thật nhỏ nhất đang hiển thị), `hasMore` (= số tin trả về === limit), `loadingOlder`.
  - Nút/scroll-to-top trigger: fetch `beforeId=oldestMessageId`, **prepend** vào list, **giữ nguyên vị trí scroll** (đo `scrollHeight` trước/sau, bù `scrollTop`).
  - `hasMore=false` khi trả về < limit → ẩn nút/loader, hiện "Đầu hội thoại".
- **Acceptance:** cuộn lên tải được tin cũ; vị trí scroll không nhảy; không tải trùng; hết tin thì dừng.
- **Edge cases:** hội thoại < 50 tin (hasMore=false ngay); spam cuộn (khoá khi `loadingOlder`); tin AI temp-id âm KHÔNG được tính vào `oldestMessageId`.

---

### GĐ4.3 — Resync đầy đủ sau reconnect

**Vấn đề:** `.withAutomaticReconnect()` có sẵn nhưng khi kết nối lại KHÔNG kéo tin phát
sinh lúc mất mạng → mất tin trên UI. (unread-badges mới refetch counts, chưa kéo nội dung.)

- **Backend (`MessageRepository`):** thêm tham số `afterId` (đối xứng 4.2):
  ```sql
  WHERE room_id = @roomId AND id > @afterId ORDER BY id ASC LIMIT @limit
  ```
  Endpoint nhận `?afterId=`.
- **Frontend (`ChatView.tsx`):**
  - Track `lastReceivedMessageId` cho hội thoại đang mở (chỉ id **thật**, bỏ qua temp âm).
  - Trong `onreconnected`: gọi `afterId=lastReceivedMessageId` cho hội thoại active, **append + dedupe theo id**, cập nhật badge (đã có refetch counts).
- **Acceptance:** ngắt mạng → người khác gửi vài tin → bật lại mạng → các tin đó hiện đủ, không trùng.
- **Edge cases:** mất mạng dài (>limit tin lỡ → lặp fetch tới khi < limit); tin đến đúng lúc reconnect (dedupe lo); hội thoại KHÔNG active chỉ cập nhật badge (chấp nhận, không force kéo nội dung).

---

### GĐ4.4 — Migration `sender_id` (enabler cho 4.5 + @mention sau này)

**Mục tiêu:** bỏ phụ thuộc `sender_name` (string) cho read-tracking/receipts/mention.
Đây là nền cho 4.5 — **làm trước 4.5**.

- **DB (`DbInitializer`, idempotent):**
  - `ALTER TABLE messages ADD COLUMN IF NOT EXISTS sender_id INT NULL REFERENCES users(id) ON DELETE SET NULL;`
  - Backfill 1 lần (best-effort): `UPDATE messages m SET sender_id = u.id FROM users u WHERE m.sender_id IS NULL AND m.is_ai = false AND m.sender_name = u.username;`
  - `CREATE INDEX IF NOT EXISTS idx_messages_sender_id ON messages(sender_id);`
  - Tin AI: `sender_id` để NULL.
- **Backend:** `Message` model thêm `SenderId (int?)`; `SaveNewMessage` set `sender_id`. Giữ `sender_name` cho hiển thị/back-compat. Caller ở `ChatHub` truyền `sender_id` từ user đang gửi.
- **Acceptance:** tin mới có `sender_id`; tin cũ backfill nơi username khớp; tin AI NULL không lỗi.
- **Edge cases:** username đổi sau khi gửi (backfill cũ vẫn đúng theo id); user bị xóa (`ON DELETE SET NULL`).

---

### GĐ4.5 — Trạng thái tin nhắn: Đã gửi / Đã xem (DM trước)

**Mục tiêu:** người gửi biết tin đã tới/đã xem chưa. Tái dùng hạ tầng unread
(`conversation_reads.last_read_message_id`) + `sender_id` (4.4).

- **Nguyên tắc (không cần bảng mới cho "seen"):** tin `m` được người kia **đã xem** nếu
  `last_read_message_id` của họ ở hội thoại đó `>= m.id`.
- **SignalR:** mở rộng luồng `MarkRead` DM hiện có — khi người nhận mark-read, phát tới
  **người gửi** trạng thái seen (vd `SeenUpdate {privateChatUserId, lastReadMessageId}`),
  để UI người gửi cập nhật "Đã xem" realtime.
- **"Đã gửi":** tin đã `SaveNewMessage` thành công = Sent (tick đơn). ("Delivered" theo
  presence là optional — có thể gộp sau, đừng làm phức tạp giai đoạn này.)
- **Frontend:** dưới **tin cuối cùng của chính mình** trong DM, hiện tick: "Đã gửi" →
  "Đã xem" (teal). Chỉ cần trên tin mới nhất của mình (kiểu Messenger).
- **Acceptance:** A gửi cho B; A thấy "Đã gửi"; khi B mở hội thoại, A thấy "Đã xem" realtime.
- **Edge cases:** nhiều tab (đồng bộ qua `ReadUpdate` sẵn có); B chưa từng mở (chưa seen);
  **group/room receipts để sau** (tốn kém, defer — ghi `TODOS.md`).

---

### GĐ4.6 — Web Push / OS Notification  (outline — chi tiết khi tới)

**Mục tiêu:** chống bỏ lỡ khi tab đóng/không focus (CEO #3, đã defer ở `TODOS.md`).

- Service Worker + Web Push API (VAPID keys).
- Backend: bảng `push_subscriptions (user_id, endpoint, p256dh, auth)`; gửi push khi có tin
  mới mà người nhận **offline hoặc tab không focus** (dựa `PresenceTracker`).
- Frontend: xin quyền Notification, đăng ký SW, lưu subscription lên server.
- **Lưu ý:** đây là mục lớn — khi tới, quay lại Claude để spec chi tiết + threat-model trước khi code.

---

## GĐ5 — Backlog tính năng giống Zalo/Messenger/Telegram (sau khi nền tảng vững)

Thứ tự ưu tiên đề xuất (chi tiết spec hóa khi GĐ4 xong):

1. **Reactions (emoji)** — bảng `message_reactions(message_id, user_id, emoji)`, SignalR broadcast toggle. Nhỏ, sướng, ROI cao.
2. **Reply / Quote** — `messages.reply_to_id (int?)`, UI hiện trích dẫn.
3. **Sửa / Xóa tin** — `messages.edited_at`, soft-delete `deleted_at`; SignalR `MessageEdited` / `MessageDeleted`.
4. **Gửi ảnh / file** — endpoint upload + lưu trữ (local/blob), `message.type` + metadata. (Mục lớn — Claude spec trước.)
5. **Tìm kiếm tin nhắn** — Postgres full-text (`tsvector`) trên `content`.
6. **@mention** — tận dụng `sender_id`/user lookup, highlight + notify.
7. **Ghim tin (pin) / Chuyển tiếp (forward)** — bổ sung sau.

---

## Phụ lục: dọn nợ kỹ thuật (làm xen khi tiện)

- Xóa `MessageRepository.GetOldMessages` (dead code, không caller) — hoặc tái dùng cho keyset 4.2 rồi đặt lại tên cho đúng.
- `TempMessageId` dùng `int` — underflow lý thuyết sau ~2 tỷ lần, không đáng kể (để nguyên).
