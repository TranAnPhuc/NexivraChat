# Design Spec — Tài liệu đọc hiểu code NexivraChat (1 file HTML tự chứa)

**Ngày:** 2026-06-24
**Dự án:** NexivraChat — Real-time AI Chat Copilot
**Mục tiêu:** Tạo MỘT file `.html` tự chứa, mở bằng double-click chạy offline, giúp người **chưa biết lập trình** (C#/.NET, React/TypeScript) **đọc hiểu được code và nắm luồng hoạt động** của hệ thống, kèm ví dụ code thật từ dự án.

---

## 1. Mục tiêu & đối tượng

- Người đọc: chưa có kinh nghiệm với C#/.NET hay React/TypeScript, muốn hiểu code dự án và luồng chạy bên trong.
- Kết quả: sau khi đọc, người đọc nhìn vào một file trong dự án và hiểu đại ý nó làm gì, dữ liệu đi qua đâu khi đăng nhập / gửi tin / gọi AI.
- Toàn bộ nội dung bằng **tiếng Việt**, giọng văn đời thường, tránh thuật ngữ chưa giải thích.

## 2. Global Constraints

- **Một file duy nhất**: `docs/huong-dan-doc-code.html`. Mở bằng double-click, **chạy offline**, không cần server, không cần cài đặt.
- **Không phụ thuộc internet / CDN**: CSS và JS nhúng trực tiếp trong file; KHÔNG `<link>`/`<script src>` ra ngoài; KHÔNG tải font/thư viện ngoài. (Tô màu cú pháp làm thủ công bằng `<span>` + CSS, không dùng highlight.js.)
- **Code trích trong tài liệu phải khớp code thật** trong repo tại thời điểm viết; mỗi đoạn ghi rõ đường dẫn file (vd `backend/.../ChatHub.cs`). Cho phép rút gọn (bỏ bớt dòng) nhưng không bịa.
- Không sửa code nguồn của ứng dụng; đây là tài liệu thuần tuý (chỉ thêm 1 file HTML + cập nhật context.md).
- Tông màu tài liệu nên dễ đọc; có thể tham chiếu phong cách teal của app nhưng KHÔNG bắt buộc dùng token CSS của app (file độc lập).

## 3. Định dạng & trải nghiệm

- HTML5 + CSS nội tuyến trong `<style>` + một đoạn JS nhỏ trong `<script>` (vanilla, không thư viện).
- Tính năng JS tối thiểu:
  - **Mục lục (TOC)** cố định/đầu trang, bấm để nhảy tới từng phần.
  - **Gập/mở** cho mỗi phần lớn (collapsible) để dễ lướt.
  - **Nút "Copy"** ở mỗi khối code.
- Code hiển thị trong `<pre><code>`, tô màu thủ công (từ khóa, chuỗi, chú thích) bằng `<span class="...">`.
- Mỗi đoạn code đi kèm: tiêu đề "📄 đường dẫn file", rồi "Giải thích" bằng gạch đầu dòng tiếng Việt.
- "Hộp khái niệm" (callout box) màu khác để giải nghĩa thuật ngữ ngay tại chỗ gặp.

## 4. Bố cục nội dung (các phần)

1. **Mở đầu & cách dùng tài liệu** — đọc theo thứ tự, ý nghĩa các hộp màu.
2. **Tổng quan kiến trúc** — sơ đồ (vẽ bằng HTML/CSS hoặc SVG nội tuyến): Trình duyệt (React) ⇄ Web API + SignalR (.NET) ⇄ PostgreSQL; và ⇄ Gemini API. Giải thích "frontend", "backend", "database", "API" là gì.
3. **5 phút làm quen 2 ngôn ngữ (đủ để đọc code):**
   - C#: `class`, `method`, kiểu dữ liệu, `async`/`await`, namespace — mỗi mục 1 ví dụ ngắn (ưu tiên trích từ dự án).
   - React/TypeScript: `component`, `useState`, `props`, JSX, `import` — ví dụ ngắn từ dự án.
4. **Luồng 1 — Đăng nhập/Đăng ký:** `LoginView.tsx` (gửi form) → `api.ts` (Axios) → `AuthController.cs` (`/auth/register`, `/auth/login`) → `PasswordHasher`/BCrypt → `TokenService.cs` (JWT HS256) → lưu `localStorage`. Giải thích: API REST, JWT, băm mật khẩu.
5. **Luồng 2 — Vào phòng & tải lịch sử tin nhắn:** `ChatView.tsx` gọi `/rooms`, `/rooms/{id}/messages` → `RoomsController.cs` → `RoomRepository`/`MessageRepository` (Dapper) → SQL trên PostgreSQL. Giải thích: REST có tham số, Dapper, câu lệnh SQL.
6. **Luồng 3 — Chat realtime với SignalR:** kết nối hub (`ChatView` `HubConnectionBuilder`) → `ChatHub.SendMessage` lưu DB rồi `Clients.Group(...).SendAsync("ReceiveMessage", ...)` → client `connection.on('ReceiveMessage', ...)`. Kèm presence/typing (`PresenceTracker`, `PresenceUpdate`, `TypingUpdate`). Giải thích: realtime, WebSocket, "đẩy" dữ liệu, nhóm phòng.
7. **Luồng 4 — Trợ lý AI @copilot (streaming):** `ChatHub` phát hiện tiền tố `@copilot` → tạo placeholder ID âm → `AiService.StreamResponseAsync` gọi Gemini `streamGenerateContent?alt=sse` → đọc dòng `data:` → `yield return` từng đoạn → `ReceiveAiToken` → client nối chữ dần → `ReceiveAiComplete`. Giải thích: streaming/SSE, `IAsyncEnumerable`, vì sao chữ hiện dần.
8. **Từ điển thuật ngữ** — bảng: API, REST, JWT, hash/BCrypt, SignalR, WebSocket, Dapper, SQL, async/await, stream/SSE, component, state/props, JSX, DI — mỗi mục 1–2 câu tiếng Việt.
9. **Bản đồ file** — danh sách file quan trọng (backend + frontend) kèm 1 dòng "làm gì", bám theo `context.md`.

## 5. Cách chọn & trình bày code

- Ưu tiên trích các đoạn ngắn, tiêu biểu cho mỗi luồng (không dán nguyên file dài).
- Mỗi đoạn: nêu file + (nếu hữu ích) số dòng tương đối; rút gọn bằng `// ...` khi cần.
- Sau code: 3–6 gạch đầu dòng giải thích "đoạn này làm gì, biến này là gì, kết quả đi đâu".
- Khi xuất hiện thuật ngữ lần đầu → hộp khái niệm giải nghĩa, các lần sau chỉ nhắc tên.

## 6. Xử lý lỗi / rủi ro nội dung

- Vì là file tĩnh, "lỗi" chủ yếu là **sai khớp với code thật** → khi viết phải đọc lại file nguồn để trích đúng, không nhớ nhầm.
- Không nhúng tài nguyên ngoài để tránh hỏng khi offline.
- Không đưa bí mật (mật khẩu DB, API key) vào tài liệu, kể cả trong ví dụ — dùng giá trị giả như `***`.

## 7. Kiểm thử

- Mở `docs/huong-dan-doc-code.html` bằng trình duyệt (double-click): trang hiển thị đúng tiếng Việt, không lỗi font.
- Ngắt mạng internet rồi mở lại: vẫn hiển thị đầy đủ (không phụ thuộc CDN).
- Bấm các mục lục → nhảy đúng phần; gập/mở hoạt động; nút Copy code hoạt động.
- Soát: mọi đoạn code trích đều tồn tại trong repo (đường dẫn đúng); không có secret thật.

## 8. Out of scope (YAGNI)

- Không làm trang web nhiều file, không bundler, không framework tài liệu (Docusaurus…).
- Không dịch sang tiếng Anh ở đợt này.
- Không tạo bài tập tương tác/chạy code trong trình duyệt (chỉ đọc + ví dụ tĩnh).
- Không tự sinh tài liệu từ code (không cần tooling); viết tay theo nội dung đã chốt.

## 9. Tiêu chí thành công

- Một người chưa biết C#/React đọc xong **hiểu được**: hệ thống gồm những phần nào, và khi *đăng nhập / gửi tin / gọi @copilot* thì dữ liệu chạy qua các file nào theo thứ tự nào.
- File mở offline bằng double-click, đầy đủ, không lỗi, không phụ thuộc internet, không lộ secret.
