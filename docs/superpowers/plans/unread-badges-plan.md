<!-- /autoplan restore point: /c/Users/anphuc/.gstack/projects/TranAnPhuc-NexivraChat/main-autoplan-restore-20260627-221342.md -->
# Plan (đã review qua /autoplan): Unread Badges — đếm tin chưa đọc theo phòng & DM

> Mục tiêu: người dùng thấy số tin chưa đọc (badge) trên mỗi phòng và mỗi DM, để không bỏ lỡ phản hồi khi đang ở cuộc trò chuyện khác. Badge tự xóa khi mở/đọc hội thoại đó.
> Review: CEO + Design + Eng (mỗi phase 1 tiếng nói Claude subagent độc lập; Codex không cài → `[subagent-only]`). Quyết định nhỏ tự quyết theo 6 nguyên tắc; quyết định khẩu vị + 1 user-challenge để mở ở cổng phê duyệt.

## Premise (đã xác nhận hợp lệ)
Unread/“không bỏ lỡ phản hồi” là table-stakes của app chat. Vấn đề đúng. Rủi ro/regret thấp. Cả ba tiếng nói đồng ý premise hợp lệ, nhưng cảnh báo: badge chỉ **đáng tin khi việc giao tin đáng tin** (xem User Challenge UC1).

## What already exists (map)
- `messages` (id SERIAL, room_id|private_chat_id nullable, sender_name VARCHAR, is_ai, created_at). `SaveNewMessage` có populate `Id` (dùng ở `ReceiveAiComplete`, `ChatHub.cs:169`).
- DM delivery đã đúng: `SendPrivateMessage` → `Clients.Users(sender, receiver)` (`ChatHub.cs:204`) → tới người nhận bất kể đang xem chat nào.
- Mẫu phân quyền participant đã có: `UsersController.GetPrivateChatMessages` (lines 84-88: load chat, so `User1Id`/`User2Id`, else `Forbid()`). `PrivateChatRepository.GetById` (line 49) tái dùng được.
- Index hiện có: `(room_id, created_at)`, `(private_chat_id, created_at)` — KHÔNG phục vụ truy vấn `id > last_read`.
- Test hiện tại chỉ in-memory (`PresenceTrackerTests`, `TempMessageIdTests`) — CHƯA có harness DB hay SignalR hub.

## Phát hiện then chốt (hội tụ cả 3 tiếng nói)
- **[CRITICAL] Room delivery vỡ với mô hình group hiện tại.** `SendMessage` broadcast `Clients.Group(roomString)`, client chỉ `JoinRoom` phòng đang xem (`ChatView.tsx:145,163,284`). User ở phòng A không nhận tin phòng B → không thể tăng badge phòng B. Không có bảng room-membership; phòng mở cho mọi người. (Eng C1, CEO #2, phân tích của tôi)
- **[CRITICAL] MarkRead/unread thiếu phân quyền participant** cho DM. (Eng C2, context.md:259)
- **[HIGH] Schema cần partial unique index**, không phải `UNIQUE(user_id, room_id)` (NULL bị coi là distinct → trùng hàng read). (Eng H1, CEO #6)
- **[HIGH] Danh tính theo `sender_name` mong manh**; `"Ẩn danh"` trùng nhau. (Eng H2, CEO #5)
- **[HIGH] N+1 nếu tính count cho từng người nhận mỗi tin** → gửi tín hiệu nhẹ, client tăng optimistic, chỉ recompute khi mở/reconnect. (Eng H3)
- **[HIGH] Cold-start**: phòng chưa mở phải = 0 (baseline `last_read` = MAX(id) khi gặp lần đầu), tránh badge "9999". (Eng H4, Design H4, CEO #2)
- **[HIGH→MED] document.title `(N) Nexivra`** — chống bỏ lỡ khi tab nền; rẻ, giá trị cao. (CEO #3, Design M4)
- **[MED] Reconnect chưa wire** — không có `onreconnected` (kể cả rejoin phòng cũng không). (Eng M1)

## Thiết kế (đã sửa theo review)

### Data model
```sql
CREATE TABLE conversation_reads (
  user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  room_id INT REFERENCES chat_rooms(id) ON DELETE CASCADE,
  private_chat_id INT REFERENCES private_chats(id) ON DELETE CASCADE,
  last_read_message_id INT NOT NULL DEFAULT 0,
  updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
  CHECK ((room_id IS NULL) <> (private_chat_id IS NULL))   -- đúng 1 trong 2
);
CREATE UNIQUE INDEX uq_reads_room ON conversation_reads(user_id, room_id) WHERE room_id IS NOT NULL;
CREATE UNIQUE INDEX uq_reads_dm   ON conversation_reads(user_id, private_chat_id) WHERE private_chat_id IS NOT NULL;
CREATE INDEX idx_messages_room_id    ON messages(room_id, id);
CREATE INDEX idx_messages_private_id ON messages(private_chat_id, id);
```
Unread = `COUNT(messages WHERE conversation_match AND id > last_read AND sender_name <> @me)`. Upsert dùng `ON CONFLICT` nhắm đúng partial index (2 câu lệnh tách cho room/DM).

### Backend
- `ConversationReadRepository` (Dapper async, cột tường minh, snake_case — theo `MessageRepository.cs`): `UpsertLastRead`, `GetUnreadCounts(userId)` (gộp room + DM theo participant).
- `GET /api/users/unread-counts` → `{ rooms: {id:n}, privateChats: {id:n} }`. DM join `private_chats` WHERE `user1_id=@me OR user2_id=@me` (không nhận id từ client).
- Hub `MarkRead(roomId?, privateChatId?, lastRenderedMessageId)`: **verify participant** cho DM (reuse `PrivateChatRepository.GetById`); set `last_read = lastRenderedMessageId` (client truyền, tránh over-mark — Eng M2); phát `UnreadUpdate` tới các connection khác của chính user (đa tab).
- `SendMessage`/`SendPrivateMessage`: sau khi lưu, phát **tín hiệu nhẹ** `UnreadUpdate(conversationKey)` tới người nhận. Phòng: vì group không tới được, dùng kênh per-user (`Clients.Users(...)`/`Clients.All` lọc client-side) — **quyết định kênh = đã chốt: kênh per-user độc lập group** (xem sơ đồ).

### Kênh giao unread (sơ đồ kiến trúc)
```
                         có tin mới
                             │
        ┌────────────────────┼─────────────────────┐
        ▼                    ▼                       ▼
  Clients.Group(room)   (KHÔNG tới user      Clients.Users(participants)
  → chỉ ai đang ở phòng   ở phòng khác)        → DM tới đúng người
        │                                            │
        └──────────────► VẤN ĐỀ ◄────────────────────┘
                             │
        Giải pháp: kênh unread PER-USER, tách khỏi room-group
                             │
   SendMessage → UnreadUpdate(roomKey) tới Clients.Users(tất-cả-user-online)
   SendPrivateMessage → UnreadUpdate(dmKey) tới Clients.Users(receiver)
        client tăng badge optimistic; recompute khi mở/reconnect
```

### Frontend
- Load: `GET /unread-counts` → render badge ở `RoomSidebar` (antd `<Badge>`).
- `UnreadUpdate(key)` cho hội thoại KHÔNG active → tăng badge optimistic.
- Mở hội thoại → `MarkRead(...)` (truyền id tin cuối đã render) → badge về 0.
- `conn.onreconnected(() => { rejoin active room; refetch /unread-counts; refetch tin của hội thoại active })` — fold gói resync tối thiểu (UC1).
- `document.title = "(N) Nexivra Chat"` từ tổng unread; reset khi tab focus + đang ở hội thoại.

### UI/UX (đã chốt theo Design review)
- Phân cấp: **DM = badge số to (teal)**; **phòng = dot/đếm mờ** (DM "to tiếng" hơn — đúng "không bỏ lỡ phản hồi riêng").
- Màu: teal đậm `#0F766E` (light) để chữ trắng đạt tương phản; verify ≥4.5:1 cả 2 theme (KHÔNG để antd đỏ mặc định). Không tím (Purple Ban).
- `overflowCount={99}` (hiện "99+"), badge có min-width cố định để label truncate chứ không phải số.
- Zero = không badge (`showZero=false`). Hội thoại active = luôn 0.
- AI active đang stream: không đếm. AI trả lời `@copilot` ở phòng nền: đếm như tin thường (v1) — glyph bulb để sau.
- a11y: hàng là `button`/`role=button`, `aria-label="<tên>, N tin chưa đọc"`, vùng `aria-live="polite"` báo tin mới.
- mark-read khi active: chỉ khi hội thoại active + window focus + cuộn gần đáy.

## NOT in scope (defer → TODOS.md)
- Web Push / OS notification (CEO #3) — follow-up lớn, giá trị cao.
- Thêm `sender_id` vào `messages` (CEO #5/Eng H2) — migration p01 đáng làm nhưng tách riêng; v1 ghi rõ ràng buộc “username unique + [Authorize] ⇒ không anon”.
- Bulb glyph phân biệt AI-unread (Design C3) — polish.
- Denormalized unread counter (CEO #6/Eng H3) — chỉ khi phòng đông; ghi là trần scale đã biết.
- Pagination tải tin cũ, receipts đã gửi/đã xem (item Hướng 2 khác).

## Test diagram (codepath → coverage)
```
ConversationReadRepository.GetUnreadCounts   → integration test (Postgres/Testcontainers+Respawn)  [HARNESS MỚI]
ConversationReadRepository.UpsertLastRead    → integration + concurrency (2 tab → ON CONFLICT dedupe, H1)
Hub.MarkRead (participant auth)              → hub test, non-participant bị từ chối (C2)  [HARNESS MỚI]
Hub UnreadUpdate fan-out (room qua per-user) → hub test: user KHÔNG ở phòng B vẫn nhận (C1)
Cold-start baseline                          → new user, phòng nhiều tin → badge = 0, KHÔNG 9999 (H4)
Loại self / "Ẩn danh" collision              → unit/integration (H2)
AI message đếm; reconnect refetch            → frontend + hub
```
Test plan chi tiết: `~/.gstack/projects/TranAnPhuc-NexivraChat/main-test-plan-unread-badges.md`.

## Consensus tables (Codex N/A → [subagent-only])
```
CEO — Claude subagent / Codex / Consensus
1 Premise valid?            yes / N/A / subagent-only
2 Right problem?            yes-but(reframe notify) / N/A / subagent-only
3 Scope calibration?        DISAGREE(resync first) / N/A → User Challenge UC1
4 Alternatives explored?    no(client-side chưa xét) / N/A → resolved by Eng C1 (server bắt buộc)
5 Trajectory sound?         caveat(schema/scale) / N/A / subagent-only

ENG — Claude subagent / Codex / Consensus
1 Architecture sound?       no(C1 room delivery) / N/A → fix kênh per-user
2 Test coverage?            no(harness chưa có) / N/A → tạo harness
3 Performance?              risk(H3 N+1) / N/A → tín hiệu nhẹ
4 Security?                 no(C2 auth) / N/A → verify participant
5 Error/edge paths?         no(H1/H2/H4) / N/A → đã fix trong design
6 Deploy risk?              low(idempotent DbInitializer) / N/A / subagent-only

DESIGN — litmus (subagent-only): màu(C1) / hierarchy(C2) / AI semantics(C3) /
 active+99+/zero(H1-3) / cold-start(H4) / dark-contrast(M1) / a11y(M3) / tab-title(M4)
 → tất cả đã đưa vào "UI/UX đã chốt", trừ các mục taste mở ở cổng.
```

## Decision Audit Trail
| # | Phase | Decision | Class | Principle | Rationale |
|---|-------|----------|-------|-----------|-----------|
| 1 | CEO | Server-authoritative (không client-side) | Auto | P1/P5 | Eng C1: room delivery cần server; trust cần persistence |
| 2 | Eng | Kênh unread per-user tách group | Auto | P1 | Group không tới user ở phòng khác |
| 3 | Eng | Verify participant trong MarkRead/unread | Auto | P1 | Bảo mật; reuse mẫu UsersController |
| 4 | Eng | Partial unique index + CHECK + ON CONFLICT | Auto | P1/P5 | Chống trùng hàng read |
| 5 | Eng | Index (room_id,id)/(private_chat_id,id) | Auto | P3 | Phục vụ id>last_read |
| 6 | Eng | Tín hiệu nhẹ + optimistic, recompute khi mở/reconnect | Auto | P3 | Tránh N+1 hot path |
| 7 | CEO/Design | Cold-start baseline = MAX(id), never-opened = 0 | Auto | P1 | Tránh badge khổng lồ ngày đầu |
| 8 | Design | document.title "(N) Nexivra" trong scope | Auto | P2 | Blast radius, rẻ, giá trị cao |
| 9 | Eng | Tạo harness DB (Testcontainers/Respawn) + hub test | Auto | P1 | Không thể test repo/hub nếu thiếu |
| 10 | Design | DM to tiếng hơn phòng; teal đậm; 99+/zero/active rules | Auto | P1/P5 | Đúng mục tiêu + design system |
| 11 | Eng | Fold reconnect refetch (counts + tin active) vào scope | Auto* | P2 | Giảm rủi ro "badge nói dối" (xem UC1) |

## Decisions (đã chốt ở cổng phê duyệt — 2026-06-27)
- **UC1: ĐỒNG Ý** — fold resync tối thiểu vào scope (`onreconnected` refetch unread counts + tin hội thoại active).
- **T1: phòng = dot, DM = số.**
- **T2: defer `sender_id`** — v1 ghi rõ ràng buộc "username unique + `[Authorize]` ⇒ không anon"; migration `sender_id` ở TODOS.md.

---

## GSTACK REVIEW REPORT
Runs: CEO [subagent-only], Design [subagent-only], Eng [subagent-only]. Codex: unavailable (not installed). UI scope: yes (Design ran). DX scope: no (skipped).
Status: REVIEWED — auto-decisions applied (audit trail #1-11). 2 critical + several high findings folded into design.
Findings: Eng C1 (room delivery) → per-user channel; Eng C2 (auth) → participant verify; Eng H1 (schema) → partial indexes; Eng H4/Design H4/CEO#2 (cold-start) → baseline MAX(id); CEO#3/Design M4 (tab title) → in scope.
VERDICT: APPROVED (2026-06-27). UC1 accepted (resync fold-in), T1 rooms=dot/DM=number, T2 defer sender_id. Ready to implement.

NO UNRESOLVED DECISIONS
