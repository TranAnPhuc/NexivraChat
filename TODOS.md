# TODOS — NexivraChat

> Defer từ /autoplan (unread-badges review, 2026-06-27). Xem `docs/superpowers/plans/unread-badges-plan.md`.

## Hướng 2 — nền tảng (còn lại sau unread-badges)
- [ ] Pagination "tải tin cũ" (client kẹt `limit=50&offset=0`).
- [ ] Resync đầy đủ sau reconnect (unread-badges chỉ fold phần tối thiểu: refetch counts + tin active).
- [ ] Trạng thái đã gửi/đã nhận/đã xem (receipts).

## Defer từ review unread-badges
- [ ] Web Push / OS notification — chống bỏ lỡ khi app đóng (CEO #3). Giá trị cao, follow-up lớn.
- [ ] Thêm `sender_id` vào bảng `messages` — bỏ phụ thuộc `sender_name` (string) cho read-tracking/receipts/mentions (CEO #5, Eng H2). Migration riêng.
- [ ] Glyph bulb phân biệt unread do AI trả lời `@copilot` ở phòng nền (Design C3).
- [ ] Denormalized unread counter khi phòng đông — hiện chấp nhận COUNT scan, trần scale đã biết (CEO #6, Eng H3).

## Ghi chú kỹ thuật tồn (từ context.md)
- [ ] `MessageRepository.GetOldMessages` là dead code — xóa khi tiện.
- [ ] `TempMessageId` dùng `int`, lý thuyết underflow sau ~2 tỷ lần (không đáng kể).
