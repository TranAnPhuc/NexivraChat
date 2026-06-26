---
status: ACTIVE
---
# Spec: Chương 9 — "Nền tảng: tự đọc, tự nhớ, không phụ thuộc AI"

Tạo bởi /plan-ceo-review ngày 2026-06-26 · Mode: SELECTIVE EXPANSION · Approach: C (10 sao)
File đích: `docs/huong-dan-source-code.html` (thêm 1 chương, không tạo file mới)

## Vấn đề & tiền đề
Người dùng sợ "mất nền tảng trong kỷ nguyên AI". Ràng buộc thật KHÔNG phải thiếu tài liệu
tra cứu — mà là thiếu **luyện truy hồi (recall) đều đặn** và **dám làm không-AI**. Một explainer
dạy *nhận ra*; cái còn thiếu là ép *tự tái tạo*. Vì vậy chương mới là một **hệ luyện tập**, không
phải tờ tra cứu.

Mục tiêu số 1 của người dùng: **đọc được bất kỳ file nào trong project và tự giải thích.**

## Phạm vi đã chốt (qua AskUserQuestion)
- Approach C (10 sao): từ điển khái niệm + Lab tự tay + vòng truy hồi + kỉ luật không-AI.
- Cherry-pick 1 (ACCEPTED): Capstone "tự đọc 1 file LẠ".
- Cherry-pick 2 (ACCEPTED): bảng "Soi file lạ trong 60 giây".
- Đóng gói: nhồi vào file HTML hiện có, offline 100%.

## Cấu trúc chương
1. **Mở đầu:** lời dặn (dừng lại tự làm); **quy tắc vàng: làm lab KHÔNG dùng AI trước**;
   lộ trình gợi ý "2 tuần, mỗi ngày 1 thẻ"; thanh tiến độ recall (localStorage).
2. **Bảng "Soi file lạ trong 60 giây"** — checklist 6 bước, dùng xuyên suốt:
   file nhập gì/xuất gì → ai gọi nó → theo dòng dữ liệu → tìm chỗ đọc/ghi DB →
   bỏ qua chi tiết chưa cần → tóm 1 câu "file này phục vụ việc gì".
3. **10 thẻ khái niệm** — mỗi thẻ 6 nhịp:
   ① tên + 1 câu cốt lõi ② gặp ở đâu (tên hàm + file, KHÔNG dùng số dòng cứng để khỏi mục)
   ③ ví dụ đời thường ④ 🔬 Lab: dự đoán → đổi 1 dòng → chạy → quan sát → `git restore <file>`
   (đáp án "điều sẽ xảy ra" giấu trong <details>) ⑤ ✍️ Thử trang trắng: đóng doc, viết lại
   đoạn nhỏ ⑥ 🧠 Tự kiểm tra (1 câu, đáp án giấu) + ô tick "đã tự recall" (localStorage).
   - Backend: async/await+Task · DI (constructor) · generic `<T>` · LINQ · IAsyncEnumerable/
     yield/await foreach · Dapper params + JSONB `::jsonb`.
   - Frontend: useState · useEffect · useCallback+React.memo · TS interface + optional `?`.
4. **🎓 Capstone:** tự đọc `TokenService.cs` + `PrivateChatRepository.cs` (chưa giải thích) →
   viết 3 câu (làm gì / ai gọi / dữ liệu đi đâu) → đáp án giấu.

## Quyết định kỹ thuật (review)
- Predict-first: dùng `<details>`/`<summary>` (HTML thuần, không cần JS, không lộ khi cuộn).
- localStorage tick + progress: bọc try/catch — tắt localStorage vẫn dùng được, chỉ mất tracking.
- Mỗi Lab BẮT BUỘC kèm `git restore <file>` + nhắc "đừng commit thay đổi lab".
- Trỏ theo TÊN HÀM/đặc điểm, không số dòng cứng (chống stale khi code đổi).
- Nội dung neo vào code đang tồn tại thật.

## N/A (artifact HTML tĩnh)
Security / Perf / Deploy / Observability / Scaling / SPOF — không áp dụng.

## Để dành (TODOS)
- Bảng "khái niệm này ở ngôn ngữ khác" (giúp chuyển giao sang stack khác).
- Ô "nhật ký lỗi của tôi" (ghi bug đã gặp + cách sửa).

## Implementation tasks
- T1 Style + cấu trúc + lời dặn + quy tắc không-AI + mục TOC.
- T2 Bảng "Soi file lạ 60 giây" + lộ trình 2 tuần.
- T3 6 thẻ Backend (đủ 6 nhịp, lab có git restore).
- T4 4 thẻ Frontend (đủ 6 nhịp).
- T5 Capstone 2 file lạ.
- T6 JS nhỏ: tick localStorage + thanh tiến độ recall (try/catch).
