# TODOS — NexivraChat

> Defer từ /autoplan (unread-badges review, 2026-06-27). Xem `docs/superpowers/plans/unread-badges-plan.md`.
> **Roadmap thực thi (Antigravity code, thứ tự GĐ4.x):** `docs/superpowers/plans/roadmap-foundation.md` (qua /plan-ceo-review, 2026-06-28).

## Hướng 2 — nền tảng (thứ tự thực thi GĐ4)
- [x] GĐ4.1 Test harness đóng sổ unread-badges (Testcontainers/Respawn + hub test) — LÀM TRƯỚC.
- [x] GĐ4.2 Pagination "tải tin cũ" theo keyset `id` (client kẹt `limit=50&offset=0`).
- [ ] GĐ4.3 Resync đầy đủ sau reconnect (hiện chỉ fold counts + tin active).
- [ ] GĐ4.4 Migration `sender_id` (enabler cho receipts/@mention) — trước 4.5.
- [ ] GĐ4.5 Trạng thái Đã gửi/Đã xem (DM trước; group defer).
- [ ] GĐ4.6 Web Push/OS notification (mục lớn — Claude spec trước khi code).

## Defer từ review unread-badges
- [ ] Web Push / OS notification — chống bỏ lỡ khi app đóng (CEO #3). Giá trị cao, follow-up lớn.
- [ ] Thêm `sender_id` vào bảng `messages` — bỏ phụ thuộc `sender_name` (string) cho read-tracking/receipts/mentions (CEO #5, Eng H2). Migration riêng.
- [ ] Glyph bulb phân biệt unread do AI trả lời `@copilot` ở phòng nền (Design C3).
- [ ] Denormalized unread counter khi phòng đông — hiện chấp nhận COUNT scan, trần scale đã biết (CEO #6, Eng H3).

## Ghi chú kỹ thuật tồn (từ context.md)
- [ ] `MessageRepository.GetOldMessages` là dead code — xóa khi tiện.
- [ ] `TempMessageId` dùng `int`, lý thuyết underflow sau ~2 tỷ lần (không đáng kể).
