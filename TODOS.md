# TODOS — NexivraChat

> Defer từ /autoplan (unread-badges review, 2026-06-27). Xem `docs/superpowers/plans/unread-badges-plan.md`.
> **Roadmap thực thi (Antigravity code, thứ tự GĐ4.x):** `docs/superpowers/plans/roadmap-foundation.md` (qua /plan-ceo-review, 2026-06-28).

## Hướng 2 — nền tảng (thứ tự thực thi GĐ4)
- [x] GĐ4.1 Test harness đóng sổ unread-badges (Testcontainers/Respawn + hub test) — LÀM TRƯỚC.
- [x] GĐ4.2 Pagination "tải tin cũ" theo keyset `id` (client kẹt `limit=50&offset=0`).
- [x] GĐ4.3 Resync đầy đủ sau reconnect (hiện chỉ fold counts + tin active).
- [x] GĐ4.4 Migration `sender_id` (enabler cho receipts/@mention) — trước 4.5.
- [x] GĐ4.5 Trạng thái Đã gửi/Đã xem (DM trước; group defer).
- [ ] GĐ4.6 Web Push/OS notification (mục lớn — Claude spec trước khi code).

## Polish từ review GĐ4 (Claude, 2026-06-28) — không chặn
- [x] #1 Resync >50 tin: hiện chỉ kéo 1 lô 50 sau reconnect (`ChatView.tsx:471,495`) → lặp `afterId` tới khi lô <limit.
- [x] #2 Receipts dùng `senderName === username` (`ChatView.tsx:800`) thay vì `senderId` (4.4) — đổi khi tiện (cần userId của mình ở frontend).
- [x] #3 Xóa `MessageRepository.GetOldMessages` (dead code, giờ còn thêm sender_id vào).
- [x] #4 Dedupe resync dùng `findIndex` trong `filter` (O(n²)) → dùng `Set` như `loadOlderMessages`.
- [x] **Checkpoint test 4.2→5.3** — spec: `roadmap-foundation.md` mục "Checkpoint Test". Phủ ReactionRepository, Edit/SoftDelete (authz), reply snapshot, blank tin đã xóa, keyset, hub authz. Rồi `dotnet test` (Docker) xanh.

## Polish từ review GĐ5.1 (Claude, 2026-06-28)
- [x] #A Reactions thiếu kiểm tra participant — `ReactionsController.GetReactions` + Hub `ToggleReaction` cho DM.
- [ ] #B Reactions chưa có long-press mobile (chỉ hover desktop) — defer.

## GĐ5 — tính năng giống Zalo/Messenger/Telegram
- [x] GĐ5.1 Reactions (emoji) — spec: `docs/superpowers/plans/roadmap-foundation.md` mục GĐ5.1.
- [x] GĐ5.2 Reply/Quote — spec: `roadmap-foundation.md` mục GĐ5.2.
- [x] GĐ5.3 Sửa/Xóa tin nhắn — spec: `roadmap-foundation.md` mục GĐ5.3.
- [x] GĐ5.4 @mention nhắc tên — spec: `roadmap-foundation.md` mục GĐ5.4.
- [x] GĐ5.4-test Thêm test `GetUnreadMentionRoomIds` (đóng nốt kỷ luật test).
- [x] GĐ5.5 Gửi ảnh/file — spec: `roadmap-foundation.md` mục GĐ5.5 (kèm threat-model).
- [x] GĐ5.7 Typing indicator cho DM (phòng đã có; mở rộng `TypingPrivate`/`PrivateTypingUpdate`) — spec: `roadmap-foundation.md` mục GĐ5.7.
- [ ] GĐ5.6 Tìm kiếm tin nhắn trong hội thoại (ILIKE keyset + jump+highlight) — spec: `roadmap-foundation.md` mục GĐ5.6.
  - [ ] GĐ5.6-test `MessageSearchTests` (case-insensitive, loại tin đã xóa, escape wildcard).
  - [ ] Defer: nút "tải tin mới hơn" sau khi nhảy về quá khứ; search toàn cục nhiều hội thoại.

## Hardening sau GĐ5.5 (Claude review, 2026-06-28)
- [x] #C Kiểm soát truy cập file đính kèm (serve qua `/api/files` có auth + verify participant; +fix #E webp inline) — spec: `roadmap-foundation.md` mục "Hardening #C".

## Dọn nợ kỹ thuật — đợt 1 (Claude review, 2026-06-28) — spec: `roadmap-foundation.md` mục "Dọn nợ kỹ thuật — đợt 1"
- [x] #2 Receipts so theo `senderId` thay vì `senderName` (`ChatView.tsx:1102`; giữ chỗ mở hồ sơ theo tên).
- [x] #3 Xóa `MessageRepository.GetOldMessages` (dead code, không caller).
- [x] #D Check magic-bytes khi upload (chống đổi đuôi giả định dạng).

## Defer từ GĐ5.5 (Threat-Model & File Cleanup)
- [ ] Rate-limit upload theo user ID (chống spam đĩa).
- [ ] Quét virus/malware tự động cho file tải lên.
- [ ] Dọn dẹp file mồ côi (khi tin nhắn có đính kèm bị xóa/thu hồi).

## Defer từ review unread-badges
- [ ] Web Push / OS notification — chống bỏ lỡ khi app đóng (CEO #3). Giá trị cao, follow-up lớn.
- [ ] Thêm `sender_id` vào bảng `messages` — bỏ phụ thuộc `sender_name` (string) cho read-tracking/receipts/mentions (CEO #5, Eng H2). Migration riêng.
- [ ] Glyph bulb phân biệt unread do AI trả lời `@copilot` ở phòng nền (Design C3).
- [ ] Denormalized unread counter khi phòng đông — hiện chấp nhận COUNT scan, trần scale đã biết (CEO #6, Eng H3).

## Ghi chú kỹ thuật tồn (từ context.md)
- [ ] `TempMessageId` dùng `int`, lý thuyết underflow sau ~2 tỷ lần (không đáng kể).
