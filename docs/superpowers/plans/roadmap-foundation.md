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

1. **Reactions (emoji)** — xem spec chi tiết **GĐ5.1** bên dưới. Nhỏ, sướng, ROI cao.
2. **Reply / Quote** — `messages.reply_to_id (int?)`, UI hiện trích dẫn.
3. **Sửa / Xóa tin** — `messages.edited_at`, soft-delete `deleted_at`; SignalR `MessageEdited` / `MessageDeleted`.
4. **Gửi ảnh / file** — endpoint upload + lưu trữ (local/blob), `message.type` + metadata. (Mục lớn — Claude spec trước.)
5. **Tìm kiếm tin nhắn** — Postgres full-text (`tsvector`) trên `content`.
6. **@mention** — tận dụng `sender_id`/user lookup, highlight + notify.
7. **Ghim tin (pin) / Chuyển tiếp (forward)** — bổ sung sau.

---

---

## GĐ5.1 — Reactions (emoji) trên tin nhắn  [spec chi tiết]

**Mục tiêu:** thả/cởi cảm xúc (👍 ❤️ 😂 😮 😢 🙏) lên 1 tin nhắn (cả phòng & DM),
realtime cho mọi người trong hội thoại. Toggle: bấm lại cùng emoji = gỡ.

- **DB (`DbInitializer`, idempotent):**
  ```sql
  CREATE TABLE IF NOT EXISTS message_reactions (
      message_id INT NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
      user_id    INT NOT NULL REFERENCES users(id)    ON DELETE CASCADE,
      emoji      VARCHAR(16) NOT NULL,
      created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
      PRIMARY KEY (message_id, user_id, emoji)
  );
  CREATE INDEX IF NOT EXISTS idx_reactions_message ON message_reactions(message_id);
  ```
  PK (message_id, user_id, emoji) = mỗi user 1 emoji/tin đúng 1 lần (toggle dựa vào đây).
- **Backend:**
  - `Models/MessageReaction.cs` + DTO `ReactionSummary { int MessageId; string Emoji; int Count; bool MineReacted }`.
  - `ReactionRepository`:
    - `ToggleReaction(messageId, userId, emoji) → bool reacted` — `INSERT ... ON CONFLICT DO NOTHING`; nếu 0 row (đã tồn tại) thì `DELETE` và trả `false`, ngược lại trả `true`. (Hoặc check tồn tại rồi insert/delete trong 1 transaction.)
    - `GetReactionsForMessages(IEnumerable<int> messageIds, int currentUserId) → List<ReactionSummary>` — `GROUP BY message_id, emoji`, `COUNT(*)`, `bool_or(user_id = @me) AS MineReacted`. Liệt kê cột tường minh, async.
  - `LookupConversation(messageId) → (int? roomId, int? privateChatId)` (để Hub biết phát cho ai) — query `SELECT room_id, private_chat_id FROM messages WHERE id=@id`.
  - Endpoint nạp reaction theo lô khi tải lịch sử: `GET /api/reactions?messageIds=1,2,3` → `List<ReactionSummary>` (verify user là participant của hội thoại chứa các tin — tối thiểu: chỉ trả reaction của tin user có quyền xem; đơn giản nhất là tin thuộc phòng công khai hoặc DM user là thành viên).
- **SignalR (`ChatHub`):**
  - `ToggleReaction(int messageId, string emoji)`:
    1. Lấy userId từ `Context.UserIdentifier`.
    2. Validate `emoji` thuộc whitelist (👍❤️😂😮😢🙏) — chặn rác.
    3. `var reacted = await _reactionRepo.ToggleReaction(...)`.
    4. `LookupConversation(messageId)` → nếu room: `Clients.Group(roomId).SendAsync("ReactionUpdate", payload)`; nếu DM: `Clients.Users(user1, user2)` (lấy 2 user của private_chat).
    5. Payload: `{ messageId, emoji, count, userId, reacted }` (count = tổng hiện tại của emoji đó trên tin).
- **Frontend:**
  - State trong `ChatView`: `reactions: Record<number, ReactionSummary[]>` (key = messageId). Nạp khi `fetchMessageHistory`/`fetchPrivateMessageHistory` (gọi `/reactions?messageIds=...` cho các tin id > 0) và khi `loadOlderMessages` (merge thêm).
  - `MessageBubble`: hover (desktop) / long-press (mobile) hiện picker 6 emoji; render "chip" reaction dưới bong bóng (emoji + count), chip của mình bo viền teal `#0D9488`. Bấm chip/emoji → `conn.invoke('ToggleReaction', msg.id, emoji)`.
  - Nghe `ReactionUpdate` → cập nhật `reactions[messageId]`: tìm emoji, set count; nếu `count===0` xóa khỏi list; cập nhật `mineReacted` khi `payload.userId === myUserId`.
  - **Cần userId của mình ở frontend** (hiện chỉ có username/token). Lấy từ JWT claim `nameidentifier` (decode token) hoặc thêm 1 endpoint `/users/me`. Tiện thể fix luôn polish #2 (receipts dùng senderId).
- **Acceptance:**
  - A thả 👍 lên tin → B thấy chip "👍 1" realtime; A bấm lại → gỡ, chip biến mất.
  - Nhiều người cùng emoji → count cộng dồn; chip của chính mình có viền teal.
  - Reaction còn nguyên sau khi reload (nạp từ DB); hoạt động ở cả phòng & DM.
- **Edge cases:** thả lên tin AI (cho phép — AI tin có id thật); thả lên tin temp-id âm (CHẶN, chỉ cho id>0); emoji ngoài whitelist (server từ chối); spam toggle nhanh (PK + ON CONFLICT chịu được, UI optimistic rồi đồng bộ theo ReactionUpdate).
- **KHÔNG làm giai đoạn này:** reaction tùy biến/emoji picker đầy đủ, danh sách "ai đã thả" chi tiết (chỉ cần count + mine). Defer.

---

## GĐ5.2 — Reply / Quote (trả lời trích dẫn tin)  [spec chi tiết]

**Mục tiêu:** trả lời một tin cụ thể, hiện khối trích dẫn (tên + đoạn nội dung) phía
trên tin trả lời. Cả phòng & DM, realtime. Giống Zalo/Telegram.

- **DB (`DbInitializer`, idempotent):**
  `ALTER TABLE messages ADD COLUMN IF NOT EXISTS reply_to_id INT NULL REFERENCES messages(id) ON DELETE SET NULL;`
  (Tin gốc bị xóa → `reply_to_id` về NULL, không vỡ.)
- **Backend:**
  - `Message` model thêm: `ReplyToId (int?)` (cột thật) + `ReplyToSenderName (string?)`, `ReplyToContent (string?)` (**chỉ projection khi đọc**, không insert).
  - `MessageRepository`:
    - `SaveNewMessage`: thêm `reply_to_id` vào INSERT.
    - `GetMessagesByRoom`/`GetMessagesByPrivateChat`: LEFT JOIN lấy snapshot tin gốc:
      ```sql
      SELECT m.id, m.room_id, m.private_chat_id, m.sender_id, m.sender_name, m.content,
             m.created_at, m.is_ai, m.reply_to_id,
             r.sender_name AS ReplyToSenderName,
             LEFT(r.content, 120) AS ReplyToContent
      FROM messages m
      LEFT JOIN messages r ON m.reply_to_id = r.id
      WHERE ... ORDER BY ...
      ```
      (Áp cho cả nhánh `beforeId` và `afterId`. Cột tường minh, async.)
  - Hub `SendMessage(int roomId, string content, int? replyToId = null)` và
    `SendPrivateMessage(int receiverId, string content, int? replyToId = null)`:
    set `ReplyToId` khi lưu; sau khi `SaveNewMessage`, nếu `replyToId>0` thì lookup nhanh
    tin gốc (`sender_name` + `LEFT(content,120)`) gán vào object broadcast để client hiện
    quote ngay (không cần fetch thêm). Chặn `replyToId<=0` (temp-id).
- **Frontend:**
  - State `replyingTo: Message | null`. Nút "Trả lời" trên `MessageBubble` (cạnh nút Dịch) → set `replyingTo`.
  - Trên input hiện thanh quote ("Đang trả lời @Tên: đoạn trích…" + nút ✕ hủy). Khi gửi truyền `replyToId = replyingTo.id`, gửi xong clear.
  - `MessageBubble`: nếu `msg.replyToId`, render khối quote nhỏ phía trên nội dung (viền trái teal, tên + `ReplyToContent`). Bấm quote → scroll tới tin gốc nếu đang load (`document.getElementById` hoặc ref theo id); nếu không có thì bỏ qua êm.
  - `handleSendMessage` nhận thêm replyToId; `conn.invoke('SendMessage', roomId, text, replyToId)` / `SendPrivateMessage`.
- **Acceptance:** trả lời hiện quote ở cả phòng & DM, realtime; quote còn sau reload; bấm quote cuộn tới tin gốc (nếu đã tải); hủy reply hoạt động.
- **Edge cases:** trả lời tin AI (cho phép); trả lời tin đã xóa sau này (`reply_to_id` NULL → ẩn quote hoặc hiện "Tin đã bị xóa"); trả lời tin chưa tải (quote vẫn hiện nhờ snapshot, chỉ không scroll được); reply lồng nhau chỉ hiện 1 cấp (KHÔNG đệ quy).
- **KHÔNG làm:** thread/độ sâu nhiều cấp, jump-and-highlight cầu kỳ. Defer.

---

## GĐ5.3 — Sửa / Xóa tin nhắn  [spec chi tiết]

**Mục tiêu:** người gửi sửa nội dung hoặc thu hồi (xóa mềm) tin của chính mình.
Cả phòng & DM, realtime. Chỉ chủ tin được sửa/xóa (authz theo `sender_id` — enabler 4.4).

- **DB (`DbInitializer`, idempotent):**
  ```sql
  ALTER TABLE messages ADD COLUMN IF NOT EXISTS edited_at  TIMESTAMP NULL;
  ALTER TABLE messages ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMP NULL;
  ```
  Xóa mềm (giữ row, set `deleted_at`) — không mất tham chiếu reply/receipts.
- **Backend:**
  - `Message` model thêm `EditedAt (DateTime?)`, `DeletedAt (DateTime?)`.
  - **Mọi SELECT lịch sử KHÔNG được lộ nội dung tin đã xóa:**
    `CASE WHEN m.deleted_at IS NOT NULL THEN '' ELSE m.content END AS Content`,
    và snapshot reply: `CASE WHEN r.deleted_at IS NOT NULL THEN NULL ELSE LEFT(r.content,120) END AS ReplyToContent`.
    Vẫn trả `edited_at`, `deleted_at` để client render nhãn/tombstone.
  - `MessageRepository`:
    - `EditMessage(messageId, userId, newContent) → int affected`:
      `UPDATE messages SET content=@newContent, edited_at=now() WHERE id=@messageId AND sender_id=@userId AND deleted_at IS NULL;` (authz + không sửa tin đã xóa).
    - `SoftDeleteMessage(messageId, userId) → int affected`:
      `UPDATE messages SET deleted_at=now() WHERE id=@messageId AND sender_id=@userId AND deleted_at IS NULL;`
  - Hub `EditMessage(int messageId, string newContent)` / `DeleteMessage(int messageId)`:
    - Validate `messageId>0`; `newContent` không rỗng (edit).
    - Gọi repo với `userId` từ `Context.UserIdentifier`. Nếu `affected==0` → `throw HubException("Không có quyền hoặc tin không tồn tại.")` (chặn sửa/xóa tin người khác — authz nằm ở WHERE `sender_id`).
    - `LookupConversation(messageId)` → broadcast đúng audience (room group / 2 user DM):
      `MessageEdited { messageId, newContent, editedAt }` / `MessageDeleted { messageId }`.
- **Frontend:**
  - Chỉ hiện nút **Sửa**/**Xóa** trên tin của chính mình (`senderName === username`), chưa xóa, không phải AI, `id>0`.
  - Sửa: inline input thay nội dung bong bóng → `conn.invoke('EditMessage', id, text)`. Nghe `MessageEdited` → cập nhật `content` + hiện nhãn "(đã sửa)".
  - Xóa: confirm (antd `Modal.confirm`) → `conn.invoke('DeleteMessage', id)`. Nghe `MessageDeleted` → đổi bong bóng thành tombstone "Tin đã bị xóa" (in nghiêng, muted), ẩn nút Dịch/Reply/Reaction và ẩn chip reaction.
  - Quote tới tin đã xóa → hiện "Tin đã bị xóa" thay vì nội dung.
- **Acceptance:** sửa hiện nội dung mới + "(đã sửa)" realtime cả phòng & DM; xóa hiện tombstone realtime; chỉ chủ tin sửa/xóa được; nội dung tin đã xóa KHÔNG lấy lại được qua API; quote tới tin đã xóa hiện tombstone.
- **Edge cases:** sửa thành rỗng (chặn ở client + server); sửa/xóa tin người khác (server từ chối qua `affected==0`); xóa tin đã xóa (idempotent, `affected==0` → bỏ qua êm); sửa/xóa temp-id (chặn `id>0`); xóa tin có reactions (chip ẩn theo tombstone).
- **KHÔNG làm:** lịch sử chỉnh sửa (edit history), quyền admin phòng xóa tin người khác, thời hạn sửa. Defer.

---

## GĐ5.4 — @mention nhắc tên  [spec chi tiết]

**Mục tiêu:** gõ `@` gợi ý user trong phòng → chèn `@tên`; tin có nhắc mình được
highlight + báo riêng; **mention không mất khi offline** (lưu bền, server-authoritative
như unread). Chỉ làm cho **phòng** (DM 2 người không cần mention).

- **DB (`DbInitializer`, idempotent):**
  ```sql
  CREATE TABLE IF NOT EXISTS message_mentions (
      message_id        INT NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
      mentioned_user_id INT NOT NULL REFERENCES users(id)    ON DELETE CASCADE,
      PRIMARY KEY (message_id, mentioned_user_id)
  );
  CREATE INDEX IF NOT EXISTS idx_mentions_user ON message_mentions(mentioned_user_id);
  ```
- **Backend:**
  - Parse helper: regex `@([A-Za-z0-9_]+)` trên `content`. Resolve sang user qua
    `UserRepository` (chỉ username CÓ THẬT, **bỏ chính mình**, **bỏ `@copilot`** — đó là
    trigger AI, không phải user nên tự nhiên không resolve được).
  - `MentionRepository`:
    - `SaveMentions(messageId, IEnumerable<int> userIds)` — bulk insert `ON CONFLICT DO NOTHING`.
    - `GetUnreadMentionRoomIds(userId) → List<int>` — phòng có tin nhắc mình mà `id > last_read`:
      `SELECT DISTINCT m.room_id FROM message_mentions mm JOIN messages m ON mm.message_id=m.id LEFT JOIN conversation_reads cr ON cr.user_id=@userId AND cr.room_id=m.room_id WHERE mm.mentioned_user_id=@userId AND m.room_id IS NOT NULL AND m.id > COALESCE(cr.last_read_message_id,0);`
  - Hub `SendMessage` (sau khi `SaveNewMessage`, chỉ phòng): parse → resolve → `SaveMentions`
    → với mỗi user được nhắc `Clients.User(id).SendAsync("MentionUpdate", new { roomId, messageId, fromUsername })`.
  - Endpoint `GET /api/users/mentions` → `int[]` roomIds đang có mention chưa đọc (cho cold-start/reload).
- **Frontend:**
  - **Autocomplete:** gõ `@` trong ô input phòng → dropdown lọc user theo prefix (ưu tiên user online trong phòng); chọn → chèn `@tên `. Esc/space đóng.
  - **Highlight render:** trong `MessageBubble`, tách token `@tên` tô teal `#0D9488`; nếu `@tên === username của mình` thì nhấn đậm + viền nhẹ cả bong bóng ("bạn được nhắc").
  - **Chỉ báo bền:** state `mentionRooms: Set<number>` nạp từ `GET /api/users/mentions` lúc vào app; hiện chấm "@" cạnh tên phòng ở `RoomSidebar`. Mở phòng (mark-read) → xóa khỏi set (mention coi như đã đọc khi đã đọc qua tin đó). Nghe `MentionUpdate`: nếu phòng KHÔNG active → thêm vào set + toast "{fromUsername} đã nhắc bạn ở #{phòng}".
- **Acceptance:** gõ @ ra gợi ý, chèn đúng; tin nhắc mình highlight + (nếu phòng nền) có toast; chỉ báo "@" còn sau reload; mở phòng thì hết; nhắc nhiều người cùng lúc đều nhận.
- **Edge cases:** `@tên` không tồn tại (bỏ qua, chỉ hiển thị text thường); `@` chính mình (không tự báo); `@copilot` (không tính mention, vẫn kích hoạt AI như cũ); mention trong tin bị sửa/xóa sau đó (chỉ parse lúc tạo — không cập nhật lại, chấp nhận; tin xóa → CASCADE dọn mention).
- **KHÔNG làm:** `@everyone`/`@here`, mention trong DM, hộp thư mention riêng, cập nhật mention khi edit. Defer.

---

## GĐ5.5 — Gửi ảnh / file  [spec chi tiết + threat-model]

**Mục tiêu:** đính kèm ảnh (hiện inline) và file (chip tải về) vào tin nhắn, cả phòng & DM.
Đây là mục có **rủi ro bảo mật cao nhất** (upload) → đọc kỹ threat-model.

### Luồng tổng thể
Upload KHÔNG đi qua SignalR (không tải binary qua hub). Client: (1) POST file qua REST →
nhận `{url, name, type, size}`; (2) gửi tin qua SignalR kèm metadata đính kèm.

### DB (`DbInitializer`, idempotent)
```sql
ALTER TABLE messages ADD COLUMN IF NOT EXISTS attachment_url  VARCHAR(512) NULL;
ALTER TABLE messages ADD COLUMN IF NOT EXISTS attachment_name VARCHAR(255) NULL;
ALTER TABLE messages ADD COLUMN IF NOT EXISTS attachment_type VARCHAR(100) NULL; -- mime
ALTER TABLE messages ADD COLUMN IF NOT EXISTS attachment_size BIGINT NULL;
```
`content` được phép rỗng khi tin chỉ có đính kèm. Thêm 4 field vào `Message` model + mọi SELECT lịch sử.

### Backend — endpoint upload
- `POST /api/upload` `[Authorize]`, `multipart/form-data` (field `file`). Trả `{ url, name, type, size }`.
- Lưu: filesystem `wwwroot/uploads/{yyyy}/{MM}/{guid}{ext}`; phục vụ qua `app.UseStaticFiles()`.
  (Production nên chuyển blob/CDN — **defer**, ghi chú trong code.)
- Hub `SendMessage`/`SendPrivateMessage`: thêm optional params `string? attachmentUrl=null,
  string? attachmentName=null, string? attachmentType=null, long? attachmentSize=null`
  (giữ back-compat) → lưu vào message + broadcast như cũ.

### ⚠️ Threat-model (BẮT BUỘC làm đủ)
1. **Giới hạn kích thước:** ≤ 10 MB. Chặn ở client + server + cấu hình Kestrel/Form
   (`MultipartBodyLengthLimit`, `RequestSizeLimit`). Quá cỡ → 413/400 rõ ràng.
2. **Whitelist loại file:** ảnh `image/jpeg,png,gif,webp` + tài liệu `pdf` (mở rộng sau).
   Kiểm **cả phần mở rộng LẪN content-type**; lý tưởng check magic-bytes. **Cấm SVG**
   (chứa script → XSS). Loại ngoài whitelist → từ chối.
3. **Tên file:** KHÔNG tin tên client. Sinh tên `{guid}{ext-an-toàn}`. Lưu tên gốc (đã
   sanitize) riêng ở `attachment_name` chỉ để hiển thị. Chặn path traversal (không ghép path client).
4. **Phục vụ file an toàn:** ảnh cho hiện inline; file KHÔNG phải ảnh trả kèm
   `Content-Disposition: attachment` (ép tải, không render trong origin). Set
   `X-Content-Type-Options: nosniff`. (Nếu được, phục vụ uploads từ path/subdomain riêng.)
5. **Auth:** upload yêu cầu JWT (đã có `[Authorize]`).
6. **Defer (ghi TODOS):** rate-limit theo user, quét virus, dọn file mồ côi (tin xóa → file vẫn còn).

### Frontend
- Nút kẹp giấy cạnh ô input → chọn file → validate (size/type) → upload (hiện progress) →
  preview (ảnh thu nhỏ / tên file) → gửi kèm tin (có thể kèm text).
- Render `MessageBubble`: nếu `attachment_type` là ảnh → `<img>` thumbnail bo góc, bấm phóng to
  (antd `Image` preview); ngược lại → chip "📎 {tên} ({size})" bấm tải về.
- Tin chỉ-đính-kèm (content rỗng) vẫn render đẹp.

### Acceptance
- Gửi ảnh → hiện inline + phóng to; gửi pdf → chip tải về; cả phòng & DM, realtime.
- File > 10 MB hoặc loại lạ → bị từ chối với báo lỗi rõ.
- File không-ảnh tải về (không render trong tab); SVG bị cấm.
- Đính kèm còn sau reload (lưu DB).

### Edge cases
- Upload lỗi giữa chừng (mất mạng) → báo lỗi, không gửi tin.
- Tin có đính kèm + reply + reaction cùng lúc (các tính năng độc lập, phải sống chung).
- Đính kèm trên tin AI (không áp dụng — AI chỉ text).
- Tin đính kèm bị xóa (soft-delete) → ẩn cả đính kèm (tombstone); file vật lý dọn sau (defer).

### KHÔNG làm
Nhiều file/tin, kéo-thả, dán ảnh từ clipboard, video/audio, thumbnail server-side. Defer.

---

## Hardening #C — Kiểm soát truy cập file đính kèm  [spec chi tiết]

**Mục tiêu:** file đính kèm DM chỉ người trong cuộc tải được (hiện ai có URL cũng tải).
Phục vụ file qua endpoint có auth + verify participant, thay vì static công khai.

- **Lưu ngoài wwwroot:** UploadController lưu vào `{ContentRootPath}/uploads/{yyyy}/{MM}/{guid}{ext}`
  (KHÔNG để trong `wwwroot` → không bị `UseStaticFiles` phơi ra). Trả `url = /api/files/{yyyy}/{MM}/{filename}`.
- **Gỡ phục vụ uploads qua `UseStaticFiles`** (đoạn `OnPrepareResponse` cho uploads không cần nữa;
  nếu wwwroot còn asset khác thì giữ UseStaticFiles, miễn uploads không nằm trong đó).
- **`FilesController` mới — `GET /api/files/{year}/{month}/{filename}` `[Authorize]`:**
  - Sanitize tham số: `year` = 4 chữ số, `month` = 2 chữ số, `filename` chỉ `[A-Za-z0-9._-]`
    (chống path traversal — từ chối nếu chứa `/`, `\`, `..`).
  - Tra message theo `attachment_url = '/api/files/{year}/{month}/{filename}'`:
    - Không có message → `404`.
    - `private_chat_id` → verify user hiện tại là participant (như `UsersController.GetPrivateChatMessages`); không phải → `403`.
    - `room_id` → cho qua (phòng công khai).
  - Trả `PhysicalFile`/stream với content-type theo ext, header `X-Content-Type-Options: nosniff`,
    và `Content-Disposition: attachment` nếu KHÔNG phải ảnh (ảnh: inline).
  - **Fix #E luôn:** map `.webp → image/webp` (và các ext ảnh) để webp hiện inline, không bị ép tải.
- **JWT cho `<img>`:** `<img src>`/link tải không gửi header Authorization → mở rộng
  `JwtBearerEvents.OnMessageReceived` chấp nhận `access_token` query cho path bắt đầu `/api/files`
  (giống đã làm cho `/chatHub`). Frontend gắn `?access_token={token}` vào URL ảnh/file khi render.
- **Acceptance:** ảnh/file phòng tải được bình thường; ảnh DM — người trong cuộc xem được,
  user khác gọi URL → `403`; webp hiện inline; non-ảnh vẫn ép tải; path traversal bị chặn.
- **Note:** file đã upload trước đó dạng `/uploads/...` (nếu có) — đang dev, không cần migrate.

---

## Checkpoint Test — phủ test cho 4.2→5.3  [spec chi tiết]

**Mục tiêu:** khóa độ an toàn các logic đã ship nhưng chưa có test, ưu tiên **nhánh authz**.
Theo đúng pattern sẵn có: `[Collection("DatabaseCollection")]`, `IAsyncLifetime` +
`await _fixture.ResetDatabaseAsync()` trong `InitializeAsync`, repo khởi tạo từ
`fixture.DapperContext`, arrange bằng `UserRepository.Create` / `RoomRepository.Create` /
`PrivateChatRepository.GetOrCreate` / `MessageRepository.SaveNewMessage`.

**File mới `Integration/ReactionRepositoryTests.cs`:**
- `ToggleReaction_TwiceBySameUser_AddsThenRemoves` (reacted true→count1, rồi false→count0).
- `ToggleReaction_MultipleUsers_CountsAggregate`.
- `GetReactionsForMessages_ReturnsCount_AndMineReactedTrueForSelf`.
- ⭐ `GetReactionsForMessages_DmMessage_HiddenFromNonParticipant` (user ngoài DM → list rỗng).
- `GetReactionsForMessages_RoomMessage_VisibleToAnyAuthenticatedUser`.
- `LookupConversation_ReturnsRoomId_OrPrivateChatId`.

**File mới `Integration/MessageRepositoryTests.cs`:**
- ⭐ `EditMessage_OwnMessage_UpdatesContent_AndSetsEditedAt`.
- ⭐ `EditMessage_OtherUsersMessage_ReturnsZeroAffected` (authz — không sửa được tin người khác).
- `EditMessage_AlreadyDeleted_ReturnsZeroAffected`.
- ⭐ `SoftDeleteMessage_OwnMessage_SetsDeletedAt`.
- ⭐ `SoftDeleteMessage_OtherUsersMessage_ReturnsZeroAffected` (authz).
- ⭐ `GetMessagesByRoom_DeletedMessage_BlanksContent` (nội dung tin đã xóa = "" — không lộ).
- `GetMessagesByRoom_ReplySnapshot_PopulatesSenderAndTruncatedContent`.
- `GetMessagesByRoom_ReplyToDeleted_SnapshotContentNull`.
- `GetMessagesByRoom_BeforeId_ReturnsOlderPage` + `AfterId_ReturnsNewerPage` (keyset 4.2/4.3).
- `SaveNewMessage_SetsSenderId` (4.4).

**Mở rộng `Integration/ChatHubTests.cs`** (theo pattern hub test sẵn có):
- ⭐ `ToggleReaction_NonParticipantDm_ThrowsHubException`.
- ⭐ `EditMessage_NonOwner_ThrowsHubException`.
- ⭐ `DeleteMessage_NonOwner_ThrowsHubException`.

**Acceptance:** `dotnet test` với Docker bật → **toàn bộ xanh** (gồm 13 test cũ + test mới).
Các test ⭐ là bắt buộc (authz + không lộ dữ liệu). Nếu Testcontainers không có Docker,
fixture đã có fallback Postgres local — nhưng mục tiêu là chạy full suite xanh.

---

## Phụ lục: dọn nợ kỹ thuật (làm xen khi tiện)

- `TempMessageId` dùng `int` — underflow lý thuyết sau ~2 tỷ lần, không đáng kể (để nguyên).

---

## Dọn nợ kỹ thuật — đợt 1 (Claude review, 2026-06-28)

Gom 3 việc nhẹ, làm chung 1 lượt. Không thêm tính năng, chỉ làm sạch + chắc.

### #2 — Receipts so theo `senderId` thay vì `senderName`
**Vấn đề:** `ChatView.tsx` xác định "tin của mình" bằng `senderName === username` (string),
trùng tên là sai. Từ GĐ4.4 message đã có `senderId`, và frontend đã có `currentUserId`
(decode JWT, `ChatView.tsx:85`).
**Việc:**
- `ChatView.tsx:1102` (vòng tìm `latestMyId` cho DM receipts): đổi
  `messages[i].senderName === username` → `messages[i].senderId === currentUserId`.
  Giữ fallback: nếu `senderId == null` (tin cũ chưa backfill) thì vẫn so `senderName === username`.
- Rà các chỗ khác trong `ChatView.tsx` dùng `senderName === username` để phân biệt "mình":
  những chỗ chỉ để **mở hồ sơ** (vd `handleOpenSenderProfile:149`) thì GIỮ NGUYÊN (đó là theo tên,
  đúng ngữ cảnh) — chỉ đổi chỗ liên quan receipts/"tin của mình".
**Acceptance:** 2 user trùng `username` (nếu DB cho phép) hoặc test tay: badge Đã gửi/Đã xem
chỉ gắn vào tin của đúng người đang đăng nhập.

### #3 — Xóa `MessageRepository.GetOldMessages`
**Vấn đề:** dead code, không caller (đã grep toàn repo). Keyset 4.2 dùng method khác.
**Việc:** Xóa hẳn method `GetOldMessages(int limit, int offset)` trong `MessageRepository.cs`.
Bỏ ghi chú "dead code" tương ứng trong `context.md` + `TODOS.md`.
**Acceptance:** `dotnet build` xanh (xác nhận không còn caller).

### #D — Check magic-bytes khi upload
**Vấn đề:** `UploadController` mới whitelist theo extension + content-type (client gửi, giả được).
Residual đã thấp nhờ `nosniff`, nhưng thêm kiểm magic-bytes cho chắc.
**Việc:** Trong `UploadController.UploadFile`, sau khi qua whitelist ext/mime, đọc vài byte đầu
của stream và đối chiếu signature theo ext:
- JPEG: `FF D8 FF`
- PNG: `89 50 4E 47 0D 0A 1A 0A`
- GIF: `47 49 46 38` ("GIF8")
- WEBP: byte 0–3 = `52 49 46 46` ("RIFF") **và** byte 8–11 = `57 45 42 50` ("WEBP")
- PDF: `25 50 44 46 2D` ("%PDF-")

Đọc bằng `file.OpenReadStream()` (peek header rồi reset/CopyTo), KHÔNG load cả file vào RAM.
Nếu signature không khớp ext → `BadRequest("Nội dung file không khớp định dạng khai báo.")`.
Bỏ qua khi không match được mục nào trong whitelist (đã chặn ở bước trước).
**Acceptance:** đổi tên `.exe`→`.png` rồi upload → bị từ chối; ảnh thật vẫn upload OK.

---

## GĐ5.7 — Typing indicator cho chat 1-1 (DM)

**Bối cảnh:** Typing đã chạy hoàn chỉnh cho **phòng** (hub `Typing`/`TypingUpdate`,
frontend `typingUsers` + UI dòng "… đang gõ…"). Nhưng chỉ hiện khi `activeChatType === 'room'`
(`ChatView.tsx:1134`); spec gốc defer DM vì hồi đó chưa có DM. Giờ có DM → mở rộng.

**Khác biệt định tuyến:** phòng dùng SignalR group (`Clients.OthersInGroup`), còn DM định tuyến
theo userId (`Clients.User(userId)`, như `SendPrivateMessage`/`ReceivePrivateMessage`). Vì vậy
KHÔNG tái dùng `Typing(roomId,...)` cho DM — thêm method + event riêng cho DM.

### Backend — `Hubs/ChatHub.cs`
Thêm method (đặt cạnh `Typing`):
```csharp
public async Task TypingPrivate(int receiverId, bool isTyping)
{
    var senderIdStr = Context.UserIdentifier;
    if (!int.TryParse(senderIdStr, out var senderId)) return;
    // Chỉ báo cho người nhận; không gửi lại cho chính mình
    await Clients.User(receiverId.ToString())
        .SendAsync("PrivateTypingUpdate", new { fromUserId = senderId, isTyping });
}
```
- Không đụng DB, không cần verify participant nặng (chỉ là tín hiệu nhất thời; người gửi tự
  biết mình đang chat với ai). `[Authorize]` ở class đã chặn ẩn danh.

### Frontend — `ChatView.tsx`
1. **State:** thêm `const [privateTypingFrom, setPrivateTypingFrom] = useState<number | null>(null);`
   (userId đối phương đang gõ) + `privateTypingTimeoutRef` để tự tắt nếu mất sự kiện "false".
2. **`sendTyping(isTyping)` (dòng ~818):** hiện early-return khi không phải room. Sửa thành:
   - Nếu `activeChatTypeRef.current === 'room'` và có `roomId` → `invoke('Typing', roomId, isTyping)` (như cũ).
   - Nếu `=== 'private'` và có `activeRecipientIdRef.current` → `invoke('TypingPrivate', recipientId, isTyping)`.
   - `handleInputChange` (debounce 2s) đã gọi `sendTyping` cho mọi loại chat → giữ nguyên, chỉ
     cần `sendTyping` xử lý đúng nhánh.
3. **Listener** (cạnh `conn.on('TypingUpdate', …)`):
   ```ts
   conn.on('PrivateTypingUpdate', (p: { fromUserId: number; isTyping: boolean }) => {
     // chỉ hiện nếu đúng DM đang mở
     if (activeChatTypeRef.current !== 'private' || activeRecipientIdRef.current !== p.fromUserId) return;
     if (privateTypingTimeoutRef.current) clearTimeout(privateTypingTimeoutRef.current);
     setPrivateTypingFrom(p.isTyping ? p.fromUserId : null);
     if (p.isTyping) {
       // auto-tắt sau 4s nếu không nhận được tín hiệu false (mạng rớt)
       privateTypingTimeoutRef.current = setTimeout(() => setPrivateTypingFrom(null), 4000);
     }
   });
   ```
   Nhớ `conn.off('PrivateTypingUpdate')` trong cleanup (cạnh `conn.off('TypingUpdate')` dòng ~762).
4. **Reset khi đổi hội thoại:** nơi đang `setTypingUsers([])` (dòng ~383) → thêm `setPrivateTypingFrom(null)`.
5. **UI:** cạnh khối typing của room (dòng ~1134) thêm khối DM:
   ```tsx
   {activeChatType === 'private' && privateTypingFrom !== null && (
     <div style={{/* cùng style dòng typing room */}}>
       {users.find(u => u.id === privateTypingFrom)?.username ?? 'Đối phương'} đang gõ…
     </div>
   )}
   ```

### Acceptance
- Mở DM A↔B: A gõ → B thấy "A đang gõ…"; A ngừng 2s hoặc gửi tin → tắt.
- Typing DM **không** rò sang phòng và ngược lại (đúng `activeChatType` + đúng `recipientId`).
- A đang chat DM với B, C gửi typing DM cho A (cửa sổ khác) → A KHÔNG thấy (lọc theo `recipientId`).
- `npm run build` sạch; typing phòng vẫn hoạt động như cũ (không regress).

---

## GĐ5.6 — Tìm kiếm tin nhắn trong hội thoại

**Mục tiêu:** Tìm theo từ khóa trong **hội thoại đang mở** (1 phòng hoặc 1 DM), trả danh sách
kết quả (người gửi + đoạn trích + thời gian), bấm vào → nhảy tới đúng tin và highlight.
Phạm vi v1: tìm trong **content text** (không tìm trong file/đính kèm), chỉ hội thoại đang mở
(không search toàn cục — defer).

### Backend — Repository (`MessageRepository.cs`)
Thêm 2 method, **cùng bộ cột SELECT** như `GetMessagesByRoom` (gồm reply snapshot), keyset `id DESC`:
```csharp
public async Task<List<Message>> SearchRoomMessages(int roomId, string keyword, int limit = 30, int? beforeId = null)
public async Task<List<Message>> SearchPrivateChatMessages(int privateChatId, string keyword, int limit = 30, int? beforeId = null)
```
- WHERE: `room_id = @roomId AND m.deleted_at IS NULL AND m.content ILIKE @pattern AND (@beforeId IS NULL OR m.id < @beforeId) ORDER BY m.id DESC LIMIT @limit`.
- **Escape ký tự ILIKE** trong keyword trước khi bọc `%...%`: thay `\`→`\\`, `%`→`\%`, `_`→`\_`;
  truyền `@pattern = $"%{escaped}%"` và thêm `ESCAPE '\'` vào mệnh đề ILIKE.
- Dùng tham số Dapper (chống SQL injection) — KHÔNG nội suy chuỗi vào SQL.
- KHÔNG cần `Reverse()` (kết quả search hiển thị mới→cũ là hợp lý).

### Backend — Controllers
- `RoomsController`: `[HttpGet("{id}/messages/search")]`
  `Search(int id, [FromQuery] string q, [FromQuery] int limit = 30, [FromQuery] int? beforeId = null)`.
  Validate `q` trim, độ dài ≥ 2 → nếu không, `Ok(empty list)`. Gọi `SearchRoomMessages`.
- `UsersController`: `[HttpGet("private-chat/{id}/messages/search")]` — **lặp đúng khối verify
  participant** như `GetPrivateChatMessages:111-127` (NotFound nếu chat null, Unauthorized nếu
  không parse được user, Forbid nếu không phải `User1Id/User2Id`), rồi gọi `SearchPrivateChatMessages`.

### Frontend — `ChatView.tsx`
1. **State:** `searchOpen: boolean`, `searchQuery: string`, `searchResults: Message[]`, `searching: boolean`,
   `highlightedMsgId: number | null`.
2. **UI nút kính lúp** ở header hội thoại (cạnh tên phòng/đối phương) → toggle ô search.
   Ô input + debounce ~350ms; gõ ≥2 ký tự mới gọi API. Endpoint theo `activeChatType`:
   - room: `api.get(\`/rooms/${activeRoomId}/messages/search?q=${encodeURIComponent(q)}\`)`
   - private: `api.get(\`/users/private-chat/${activePrivateChatId}/messages/search?q=${encodeURIComponent(q)}\`)`
3. **Panel kết quả** (dropdown/cột bên): mỗi item hiển thị `senderName`, đoạn `content` (cắt ~80 ký tự),
   thời gian. Rỗng → "Không tìm thấy". Đóng panel khi chọn item hoặc bấm X.
4. **Nhảy tới tin (jump):** khi bấm 1 kết quả `resultId`:
   - Nếu `messages` đã chứa `resultId` → scroll `document.getElementById(\`msg-${resultId}\`)` vào giữa
     (`scrollIntoView({ block: 'center' })`), set `highlightedMsgId = resultId`, tự xóa sau ~2s.
   - Nếu chưa có → fetch 1 trang quanh tin: gọi endpoint messages thường với
     `beforeId = resultId + 1` (để `resultId` là tin mới nhất trong trang + 49 tin cũ hơn),
     **thay** `messages` bằng trang đó, set `oldestMessageId`/`hasMore=true`, rồi scroll + highlight
     như trên (dùng `flushSync`/`requestAnimationFrame` để chắc DOM đã render trước khi scroll).
   - **Defer:** nút "tải tin mới hơn" sau khi nhảy về quá khứ (hiện chỉ có load-older). Ghi vào TODOS.
5. **Highlight:** trong `MessageBubble` (hoặc wrapper) thêm style nền nhấp nháy khi `msg.id === highlightedMsgId`
   (truyền prop xuống, hoặc set class qua DOM rồi gỡ). Dùng token màu, không hardcode tím.
6. **Reset:** đổi hội thoại → đóng search, clear `searchResults`/`searchQuery`/`highlightedMsgId`.

### Acceptance
- Gõ từ khóa trong phòng → ra đúng tin chứa từ đó (không gồm tin đã xóa).
- Bấm kết quả đang hiển thị → cuộn tới + highlight; bấm kết quả cũ (chưa load) → nạp trang rồi nhảy đúng.
- DM: người ngoài cuộc gọi endpoint search → **403** (lặp verify participant).
- Ký tự đặc biệt `%` `_` trong từ khóa được xử lý như literal (escape ILIKE), không lỗi/không match bừa.
- `dotnet build` + `npm run build` xanh.

### Test (đóng kỷ luật test)
`Integration/MessageSearchTests.cs` (theo pattern `[Collection("DatabaseCollection")]` + `IAsyncLifetime`):
- `SearchRoomMessages_FindsMatch_CaseInsensitive`.
- `SearchRoomMessages_ExcludesDeleted`.
- `SearchRoomMessages_EscapesWildcard` (chèn tin có `%`, search `%` literal chỉ khớp tin đó).
