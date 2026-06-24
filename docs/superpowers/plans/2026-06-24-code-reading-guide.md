# Code-Reading Guide (self-contained HTML) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tạo một file `docs/huong-dan-doc-code.html` tự chứa, mở offline bằng double-click, giúp người chưa biết lập trình đọc hiểu code và luồng hệ thống NexivraChat, kèm code thật từ dự án.

**Architecture:** Một file HTML tĩnh: `<style>` nội tuyến (giao diện, tô màu code thủ công), nội dung theo từng `<section>` (mỗi mục có id để mục lục nhảy tới), và một `<script>` vanilla nhỏ (gập/mở section + nút Copy). Các task xây dần vào CÙNG một file theo thứ tự: Task 1 dựng khung + CSS + JS; các task sau điền nội dung từng phần.

**Tech Stack:** HTML5, CSS thuần, JavaScript thuần (KHÔNG thư viện, KHÔNG CDN). Nội dung tiếng Việt.

## Global Constraints

- **Một file duy nhất**: `docs/huong-dan-doc-code.html`. Chạy offline, mở bằng double-click, không cần server.
- **Không phụ thuộc internet**: KHÔNG `<link rel=stylesheet href=...>` ra ngoài, KHÔNG `<script src=...>` ngoài, KHÔNG font/thư viện ngoài. Tô màu cú pháp bằng `<span>` + CSS.
- Mọi đoạn code trích phải **khớp code thật** trong repo; ghi rõ đường dẫn file. Được rút gọn bằng `// ...` nhưng KHÔNG bịa. Implementer PHẢI mở file nguồn để trích đúng.
- KHÔNG đưa secret thật (mật khẩu DB, API key) vào tài liệu — dùng `***` hoặc giá trị giả.
- Nội dung tiếng Việt, giọng đời thường, giải nghĩa thuật ngữ ngay khi xuất hiện.
- KHÔNG sửa code nguồn ứng dụng (chỉ thêm file HTML + cập nhật `context.md` ở task cuối).
- "Verification" mỗi task = mở file trong trình duyệt (offline) kiểm tra hiển thị + cấu trúc; không có unit test.

Đường dẫn file đích (tương đối gốc repo): `docs/huong-dan-doc-code.html`.

---

### Task 1: Dựng khung HTML + CSS + JS (vỏ tài liệu)

**Files:**
- Create: `docs/huong-dan-doc-code.html`

**Interfaces:**
- Produces (các task sau dựa vào): các lớp CSS `concept` (hộp khái niệm), `pre.code` + span `k/s/c/t` (tô màu), `note`, `flow`; các `<section id="...">` rỗng với id: `gioi-thieu`, `kien-truc`, `ngon-ngu`, `luong-dangnhap`, `luong-phong`, `luong-realtime`, `luong-ai`, `tu-dien`, `ban-do-file`; hàm JS gắn sẵn nút Copy cho mọi `pre.code` và toggle gập/mở cho mọi `section`.

- [ ] **Step 1: Tạo file với toàn bộ khung, CSS, JS và các section rỗng**

Tạo `docs/huong-dan-doc-code.html` với nội dung sau (điền nguyên văn):
```html
<!doctype html>
<html lang="vi">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>Hướng dẫn đọc code — NexivraChat</title>
<style>
  :root { --teal:#0d9488; --teal-soft:#f0fdfa; --ink:#0f172a; --muted:#475569; --border:#e2e8f0; --bg:#f8fafc; }
  * { box-sizing: border-box; }
  body { margin:0; background:var(--bg); color:var(--ink); font-family: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif; line-height:1.65; }
  .wrap { max-width: 980px; margin: 0 auto; padding: 24px 18px 80px; }
  h1 { font-size: 30px; margin: 8px 0 4px; }
  h2 { font-size: 22px; color: var(--teal); border-bottom:2px solid var(--teal-soft); padding-bottom:6px; cursor:pointer; }
  h3 { font-size: 17px; margin-top: 22px; }
  p, li { font-size: 15.5px; }
  a { color: var(--teal); }
  .lead { color: var(--muted); font-size: 16px; }
  .toc { background:#fff; border:1px solid var(--border); border-radius:12px; padding:14px 18px; margin:18px 0 28px; }
  .toc ol { margin:6px 0 0; padding-left: 20px; }
  .toc a { text-decoration:none; }
  section { background:#fff; border:1px solid var(--border); border-radius:12px; padding:6px 20px 18px; margin:16px 0; }
  section.collapsed > :not(h2) { display:none; }
  .concept { background:var(--teal-soft); border-left:4px solid var(--teal); border-radius:0 8px 8px 0; padding:10px 14px; margin:14px 0; }
  .concept b { color:#115e59; }
  .note { background:#fff7ed; border-left:4px solid #f97316; border-radius:0 8px 8px 0; padding:10px 14px; margin:14px 0; }
  .file { font-size:12.5px; color:#fff; background:var(--teal); display:inline-block; padding:2px 8px; border-radius:6px; margin:14px 0 6px; font-family:ui-monospace,Menlo,Consolas,monospace; }
  pre.code { position:relative; background:#0f172a; color:#e2e8f0; padding:14px 16px; border-radius:10px; overflow:auto; font-family:ui-monospace,Menlo,Consolas,monospace; font-size:13px; line-height:1.5; }
  pre.code .k { color:#5eead4; } /* từ khóa */
  pre.code .s { color:#fdba74; } /* chuỗi */
  pre.code .c { color:#64748b; font-style:italic; } /* chú thích */
  pre.code .t { color:#7dd3fc; } /* kiểu/tên lớp */
  .copy { position:absolute; top:8px; right:8px; background:#1e293b; color:#cbd5e1; border:1px solid #334155; border-radius:6px; font-size:12px; padding:3px 8px; cursor:pointer; }
  .copy:hover { background:#334155; }
  .flow { display:flex; flex-wrap:wrap; align-items:center; gap:8px; margin:10px 0; }
  .flow .box { background:var(--teal-soft); border:1px solid var(--teal); color:#115e59; border-radius:8px; padding:8px 12px; font-size:13.5px; font-weight:600; }
  .flow .arrow { color:var(--teal); font-weight:700; }
  table { border-collapse:collapse; width:100%; font-size:14.5px; }
  th, td { border:1px solid var(--border); padding:8px 10px; text-align:left; vertical-align:top; }
  th { background:var(--teal-soft); color:#115e59; }
  .hint { color:var(--muted); font-size:13.5px; }
</style>
</head>
<body>
<div class="wrap">
  <h1>Hướng dẫn đọc code — NexivraChat</h1>
  <p class="lead">Tài liệu dành cho người chưa biết lập trình: hiểu hệ thống gồm những gì và dữ liệu chạy thế nào khi bạn đăng nhập, gửi tin nhắn, hay gọi trợ lý AI.</p>

  <nav class="toc">
    <b>Mục lục</b>
    <ol>
      <li><a href="#gioi-thieu">Mở đầu &amp; cách dùng tài liệu</a></li>
      <li><a href="#kien-truc">Tổng quan kiến trúc</a></li>
      <li><a href="#ngon-ngu">5 phút làm quen 2 ngôn ngữ</a></li>
      <li><a href="#luong-dangnhap">Luồng 1 — Đăng nhập</a></li>
      <li><a href="#luong-phong">Luồng 2 — Vào phòng &amp; tải tin nhắn</a></li>
      <li><a href="#luong-realtime">Luồng 3 — Chat realtime (SignalR)</a></li>
      <li><a href="#luong-ai">Luồng 4 — Trợ lý AI @copilot</a></li>
      <li><a href="#tu-dien">Từ điển thuật ngữ</a></li>
      <li><a href="#ban-do-file">Bản đồ file</a></li>
    </ol>
    <p class="hint">Mẹo: bấm vào tiêu đề mỗi phần (chữ xanh) để gập/mở. Mỗi khối code có nút “Copy”.</p>
  </nav>

  <section id="gioi-thieu"><h2>1. Mở đầu &amp; cách dùng tài liệu</h2></section>
  <section id="kien-truc"><h2>2. Tổng quan kiến trúc</h2></section>
  <section id="ngon-ngu"><h2>3. 5 phút làm quen 2 ngôn ngữ</h2></section>
  <section id="luong-dangnhap"><h2>4. Luồng 1 — Đăng nhập</h2></section>
  <section id="luong-phong"><h2>5. Luồng 2 — Vào phòng &amp; tải tin nhắn</h2></section>
  <section id="luong-realtime"><h2>6. Luồng 3 — Chat realtime (SignalR)</h2></section>
  <section id="luong-ai"><h2>7. Luồng 4 — Trợ lý AI @copilot</h2></section>
  <section id="tu-dien"><h2>8. Từ điển thuật ngữ</h2></section>
  <section id="ban-do-file"><h2>9. Bản đồ file</h2></section>
</div>

<script>
  // Nút Copy cho mỗi khối code
  document.querySelectorAll('pre.code').forEach(function (pre) {
    var btn = document.createElement('button');
    btn.className = 'copy'; btn.textContent = 'Copy';
    btn.addEventListener('click', function () {
      var code = pre.querySelector('code') || pre;
      navigator.clipboard.writeText(code.innerText).then(function(){ btn.textContent='Đã copy'; setTimeout(function(){btn.textContent='Copy';},1200); });
    });
    pre.appendChild(btn);
  });
  // Gập/mở khi bấm tiêu đề h2
  document.querySelectorAll('section > h2').forEach(function (h) {
    h.addEventListener('click', function () { h.parentElement.classList.toggle('collapsed'); });
  });
</script>
</body>
</html>
```

- [ ] **Step 2: Mở offline kiểm tra**

Mở `docs/huong-dan-doc-code.html` bằng double-click (hoặc kéo vào trình duyệt). Xác nhận: trang hiển thị tiếng Việt đúng, có mục lục với 9 mục, bấm mục lục nhảy đúng section, bấm tiêu đề xanh thì gập/mở. (Chưa có nội dung trong các section là đúng ở bước này.)

- [ ] **Step 3: Commit**

```bash
git add docs/huong-dan-doc-code.html
git commit -m "docs: scaffold self-contained code-reading guide (shell + css + js)"
```

---

### Task 2: Nội dung phần 1–3 (mở đầu, kiến trúc, làm quen ngôn ngữ)

**Files:**
- Modify: `docs/huong-dan-doc-code.html` (điền vào 3 section `#gioi-thieu`, `#kien-truc`, `#ngon-ngu`)

**Interfaces:**
- Consumes: các lớp CSS `concept`, `flow`, `pre.code` + span `k/s/c/t` từ Task 1.

- [ ] **Step 1: Điền section `#gioi-thieu`**

Sau thẻ `<h2>1. ...</h2>` của section `#gioi-thieu`, thêm:
- 1 đoạn `<p>` nói tài liệu đọc theo thứ tự từ trên xuống.
- Giải thích 2 loại hộp màu bằng ví dụ thật:
```html
<div class="concept"><b>Hộp khái niệm</b> — giải nghĩa một thuật ngữ mới (màu xanh).</div>
<div class="note">Hộp lưu ý — mẹo hoặc cảnh báo nhỏ (màu cam).</div>
```

- [ ] **Step 2: Điền section `#kien-truc` — sơ đồ + giải thích các tầng**

Thêm sơ đồ bằng HTML/CSS (lớp `flow` đã có) và phần giải thích:
```html
<div class="flow">
  <span class="box">Trình duyệt<br>(React + TypeScript)</span>
  <span class="arrow">⇄ HTTP / WebSocket ⇄</span>
  <span class="box">Máy chủ<br>(.NET 8 Web API + SignalR)</span>
  <span class="arrow">⇄ SQL ⇄</span>
  <span class="box">CSDL<br>(PostgreSQL)</span>
</div>
<div class="flow">
  <span class="box">Máy chủ</span>
  <span class="arrow">⇄ HTTPS ⇄</span>
  <span class="box">Gemini AI<br>(Google)</span>
</div>
```
Kèm các đoạn `<p>`/`<ul>` giải thích (viết tiếng Việt, các ý bắt buộc):
- **Frontend** = phần chạy trong trình duyệt người dùng (giao diện). Ở đây là React + TypeScript.
- **Backend** = máy chủ xử lý logic, kiểm tra đăng nhập, lưu dữ liệu. Ở đây là .NET 8 (ngôn ngữ C#).
- **Database (CSDL)** = nơi lưu trữ lâu dài (user, phòng, tin nhắn). Ở đây là PostgreSQL, truy vấn bằng SQL.
- **API** = "cửa" để frontend gọi backend qua HTTP (ví dụ "đăng nhập", "lấy danh sách phòng").
- **SignalR/WebSocket** = kênh 2 chiều luôn mở để backend *đẩy* tin nhắn mới về cho mọi người tức thì (khác với API gọi-rồi-trả).
- 3 thư mục gốc: `backend/NexivraChatBackend/` (máy chủ), `frontend/nexivra-chat-frontend/` (giao diện). Dùng 1 `<div class="concept">` cho "API là gì" và 1 cho "Realtime là gì".

- [ ] **Step 3: Điền section `#ngon-ngu` — đọc C# và React/TS đủ để hiểu**

Hai phần con `<h3>C# (phía máy chủ)</h3>` và `<h3>React + TypeScript (phía giao diện)</h3>`. Mỗi phần: 1 đoạn `<pre class="code"><code>...</code></pre>` ví dụ ngắn (tự viết, đơn giản, KHÔNG cần trích repo) + giải thích từng phần. Tô màu thủ công: từ khóa `<span class="k">`, chuỗi `<span class="s">`, chú thích `<span class="c">`, kiểu/tên lớp `<span class="t">`.

Ví dụ C# (điền nguyên văn, người viết có thể bổ sung chú thích):
```html
<pre class="code"><code><span class="k">public class</span> <span class="t">Message</span> {       <span class="c">// một "khuôn" dữ liệu</span>
    <span class="k">public int</span> Id;                  <span class="c">// số nguyên</span>
    <span class="k">public string</span> Content;          <span class="c">// chuỗi chữ</span>
}

<span class="k">public async Task</span> <span class="t">SaveAsync</span>() {  <span class="c">// async = việc mất thời gian (vào DB)</span>
    <span class="k">await</span> db.SaveAsync();              <span class="c">// await = chờ xong rồi đi tiếp</span>
}</code></pre>
```
Giải thích các ý: `class` là khuôn dữ liệu; `public` = dùng được từ nơi khác; kiểu đứng trước tên (`int Id`); `async/await` cho việc chờ (gọi DB, gọi mạng).

Ví dụ React/TS (điền nguyên văn):
```html
<pre class="code"><code><span class="k">const</span> [text, setText] = <span class="t">useState</span>(<span class="s">''</span>); <span class="c">// "ô nhớ" của giao diện</span>

<span class="k">return</span> (
  &lt;input value={text}
    onChange={(e) =&gt; setText(e.target.value)} /&gt;  <span class="c">// gõ -> cập nhật ô nhớ</span>
);</code></pre>
```
Giải thích: **component** = một mảnh giao diện viết bằng hàm; **useState** = ô nhớ, đổi giá trị thì giao diện vẽ lại; **JSX** = cú pháp viết HTML ngay trong code; `{ }` = nhúng giá trị động. Dùng 1 `<div class="concept">` cho "component/state".

- [ ] **Step 4: Mở offline kiểm tra**

Mở lại file: 3 phần đầu có nội dung, sơ đồ kiến trúc hiển thị, khối code có màu và nút Copy chạy. Không có lỗi hiển thị.

- [ ] **Step 5: Commit**

```bash
git add docs/huong-dan-doc-code.html
git commit -m "docs: add intro, architecture overview and language primer sections"
```

---

### Task 3: Nội dung Luồng 1 (đăng nhập) & Luồng 2 (phòng/tin nhắn)

**Files:**
- Modify: `docs/huong-dan-doc-code.html` (section `#luong-dangnhap`, `#luong-phong`)
- Đọc để trích đúng: `frontend/nexivra-chat-frontend/src/views/LoginView.tsx`, `frontend/nexivra-chat-frontend/src/services/api.ts`, `backend/NexivraChatBackend/Controllers/AuthController.cs`, `backend/NexivraChatBackend/Services/TokenService.cs`, `backend/NexivraChatBackend/Controllers/RoomsController.cs`, `backend/NexivraChatBackend/Repositories/MessageRepository.cs`, `frontend/.../src/views/ChatView.tsx`

**Interfaces:**
- Consumes: lớp CSS từ Task 1.

- [ ] **Step 1: Section `#luong-dangnhap` — sơ đồ luồng + code thật + giải thích**

Mở 4 file nguồn (LoginView.tsx, api.ts, AuthController.cs, TokenService.cs) và TRÍCH ĐÚNG các đoạn tiêu biểu. Cấu trúc phần này:
- Sơ đồ `flow`: `Form đăng nhập (LoginView)` ⇄ `api.post('/auth/login')` ⇄ `AuthController.Login` ⇄ `kiểm mật khẩu (BCrypt)` ⇄ `TokenService cấp JWT` ⇄ `lưu localStorage`.
- Khối code 1 — `<div class="file">frontend/.../views/LoginView.tsx</div>` rồi `<pre class="code">` trích đoạn gọi `api.post(endpoint, { username, password })` và `localStorage.setItem('token', token)`. Giải thích: gửi tên/mật khẩu lên máy chủ; nhận về `token` rồi cất vào trình duyệt để các yêu cầu sau "có vé".
- Khối code 2 — `AuthController.cs` `Login`: trích đoạn `GetByUsername`, `VerifyHashedPassword`, `CreateToken`, `return Ok(... Token ...)`. Giải thích: tìm user; so khớp mật khẩu đã băm; nếu đúng thì tạo token.
- Khối code 3 — `TokenService.cs` `CreateToken`: trích đoạn tạo `SymmetricSecurityKey`, `SigningCredentials(... HmacSha256Signature)`, `claims` (Name), `Expires = ...AddDays(7)`. Giải thích ngắn.
- Hộp khái niệm: **API/REST** (cửa gọi qua HTTP), **Băm mật khẩu (hash/BCrypt)** (lưu bản xáo trộn, không lưu mật khẩu gốc), **JWT** (tấm "vé" có chữ ký, hết hạn sau 7 ngày).
- `<div class="note">`: trong tài liệu KHÔNG ghi khóa bí mật thật — thay bằng `***`.

- [ ] **Step 2: Section `#luong-phong` — REST có tham số + Dapper/SQL**

Mở `ChatView.tsx` (hàm `fetchRooms`, `fetchMessageHistory`), `RoomsController.cs`, `MessageRepository.cs` và trích đúng. Cấu trúc:
- Sơ đồ `flow`: `ChatView gọi /rooms, /rooms/{id}/messages` ⇄ `RoomsController` ⇄ `Repository (Dapper)` ⇄ `SQL` ⇄ `PostgreSQL`.
- Khối code — `ChatView.tsx`: `api.get('/rooms')` và `api.get('/rooms/${roomId}/messages?limit=50&offset=0')`. Giải thích: lấy danh sách phòng; lấy 50 tin gần nhất; `limit/offset` là phân trang.
- Khối code — `MessageRepository.cs`: trích câu Dapper lấy tin nhắn theo phòng (đúng SQL trong file). Giải thích: **Dapper** chạy câu **SQL** thẳng trên DB và map kết quả thành object C#.
- Hộp khái niệm: **Database & SQL** (kho dữ liệu + ngôn ngữ truy vấn), **Dapper** (thư viện nối C# với SQL, dự án cố ý KHÔNG dùng EF Core).

- [ ] **Step 3: Mở offline kiểm tra**

Mở file: 2 luồng hiển thị đủ sơ đồ + code màu + giải thích. Đối chiếu nhanh 1–2 đoạn code với file nguồn để chắc khớp.

- [ ] **Step 4: Commit**

```bash
git add docs/huong-dan-doc-code.html
git commit -m "docs: add login flow and rooms/messages flow with real code"
```

---

### Task 4: Nội dung Luồng 3 (SignalR realtime) & Luồng 4 (AI streaming)

**Files:**
- Modify: `docs/huong-dan-doc-code.html` (section `#luong-realtime`, `#luong-ai`)
- Đọc để trích đúng: `frontend/.../src/views/ChatView.tsx` (phần tạo `HubConnectionBuilder`, các `connection.on(...)`), `backend/.../Hubs/ChatHub.cs`, `backend/.../Services/PresenceTracker.cs`, `backend/.../Services/AiService.cs`

**Interfaces:**
- Consumes: lớp CSS từ Task 1.

- [ ] **Step 1: Section `#luong-realtime` — kết nối hub, gửi & nhận, presence/typing**

Trích đúng từ `ChatView.tsx` và `ChatHub.cs`. Cấu trúc:
- Hộp khái niệm **SignalR / WebSocket / realtime** đặt đầu: kênh 2 chiều luôn mở; máy chủ chủ động *đẩy* dữ liệu về client (không cần client hỏi liên tục).
- Khối code — `ChatView.tsx`: `new HubConnectionBuilder().withUrl(.../chatHub?access_token=...)...build()` và một `connection.on('ReceiveMessage', (msg) => { ... })`. Giải thích: client mở kết nối tới hub (kèm token để xác thực); đăng ký "khi có tin mới thì làm gì".
- Khối code — `ChatHub.cs` `SendMessage`: trích đoạn lưu DB (`_messageRepository.SaveNewMessage`) rồi `Clients.Group(roomString).SendAsync("ReceiveMessage", userMessage)`. Giải thích: máy chủ nhận tin, lưu lại, rồi phát cho mọi người trong phòng.
- Đoạn ngắn về presence/typing: trích `PresenceUpdate`/`TypingUpdate` (tên sự kiện) + 1–2 dòng `PresenceTracker` (đếm theo `connectionId`, nhiều tab vẫn tính 1 người). Giải thích vì sao "đang gõ…" và "N người online" cập nhật tức thì.
- Sơ đồ `flow`: `Bạn gửi` ⇄ `ChatHub.SendMessage` ⇄ `lưu DB` ⇄ `phát ReceiveMessage cho cả phòng` ⇄ `mọi người thấy ngay`.

- [ ] **Step 2: Section `#luong-ai` — phát hiện @copilot, gọi Gemini, stream từng chữ**

Trích đúng từ `ChatHub.cs` (nhánh xử lý `@copilot`) và `AiService.cs`. Cấu trúc:
- Hộp khái niệm **Streaming / SSE**: thay vì chờ AI viết xong cả đoạn, máy chủ nhận và đẩy về **từng mẩu chữ** ngay khi có, nên bạn thấy chữ hiện dần.
- Khối code — `ChatHub.cs`: trích đoạn `if (content.Trim().StartsWith("@copilot"...))`, tạo `tempAiMessageId` (số âm) gửi placeholder, vòng `await foreach (var token in _aiService.StreamResponseAsync(...))` → `SendAsync("ReceiveAiToken", tempAiMessageId, token)`, rồi `ReceiveAiComplete`. Giải thích: phát hiện lệnh; tạo "bong bóng rỗng" với ID tạm; mỗi mẩu chữ tới thì đẩy về và nối vào bong bóng; xong thì chốt và thay ID thật.
- Khối code — `AiService.cs`: trích đoạn URL `...streamGenerateContent?alt=sse&key=***`, vòng đọc `while ((line = await reader.ReadLineAsync()) != null)` lọc dòng `data:` và `yield return textChunk`. Giải thích: gọi Gemini ở chế độ SSE; mỗi dòng `data:` là một mẩu JSON; lấy chữ ra trả về dần. Nhắc `IAsyncEnumerable` = "dòng chảy" trả về nhiều mẩu theo thời gian. Thay key thật bằng `***`.
- Hộp `note`: nếu không cấu hình API key, `AiService` chạy "chế độ DEMO" trả lời giả lập.

- [ ] **Step 3: Mở offline kiểm tra**

Mở file: 2 luồng hiển thị đủ; đối chiếu nhanh đoạn `@copilot`/`alt=sse` với file nguồn để chắc khớp; xác nhận KHÔNG có key/secret thật trong tài liệu.

- [ ] **Step 4: Commit**

```bash
git add docs/huong-dan-doc-code.html
git commit -m "docs: add SignalR realtime and AI streaming flow sections"
```

---

### Task 5: Từ điển thuật ngữ + Bản đồ file + cập nhật context.md

**Files:**
- Modify: `docs/huong-dan-doc-code.html` (section `#tu-dien`, `#ban-do-file`)
- Modify: `context.md` (gốc repo)

- [ ] **Step 1: Section `#tu-dien` — bảng thuật ngữ**

Thêm `<table>` (lớp bảng đã có CSS) với 2 cột "Thuật ngữ" / "Giải thích ngắn (tiếng Việt)", gồm các dòng: API, REST, JWT, Băm mật khẩu (Hash/BCrypt), SignalR, WebSocket, Realtime, Dapper, SQL, PostgreSQL, async/await, Stream/SSE, Component, State (useState), Props, JSX, DI (Dependency Injection). Mỗi mục 1–2 câu, đúng theo cách đã giải thích ở các luồng (không mâu thuẫn).

- [ ] **Step 2: Section `#ban-do-file` — danh sách file + vai trò**

Thêm `<table>` 2 cột "File" / "Làm gì", bám theo `context.md`. Tối thiểu gồm: `AuthController.cs`, `RoomsController.cs`, `ChatHub.cs`, `TokenService.cs`, `AiService.cs`, `PresenceTracker.cs`, `DapperContext.cs`, `DbInitializer.cs`, `UserRepository.cs`, `RoomRepository.cs`, `MessageRepository.cs`, `Program.cs`; và frontend `App.tsx`, `LoginView.tsx`, `ChatView.tsx`, `RoomSidebar.tsx`, `CopilotPanel.tsx`, `ThemeContext.tsx`, `api.ts`. Mô tả 1 dòng mỗi file (đồng bộ với `context.md`).

- [ ] **Step 3: Cập nhật `context.md`**

Thêm một dòng/mục ngắn trong `context.md` chỉ ra có tài liệu đọc code cho người mới: đường dẫn `docs/huong-dan-doc-code.html`, mở offline bằng double-click.

- [ ] **Step 4: Mở offline + ngắt mạng kiểm tra tổng thể**

Tắt Wi-Fi/mạng, mở lại `docs/huong-dan-doc-code.html`: toàn bộ 9 phần hiển thị đầy đủ, không hỏng (chứng tỏ không phụ thuộc internet). Rà soát lần cuối: không có secret thật; các đường dẫn file đúng.

- [ ] **Step 5: Commit**

```bash
git add docs/huong-dan-doc-code.html context.md
git commit -m "docs: add glossary, file map and link guide from context.md"
```

---

## Notes
- Toàn bộ là tài liệu tĩnh; không thêm dependency, không sửa code ứng dụng (trừ 1 dòng liên kết trong `context.md`).
- Implementer các Task 3–4 BẮT BUỘC mở file nguồn để trích code đúng; nếu code thực tế khác mô tả, ưu tiên code thật và chú thích cho khớp.
- Giữ giọng văn nhất quán, tiếng Việt, cho người chưa biết lập trình.
