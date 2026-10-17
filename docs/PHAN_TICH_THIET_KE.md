# Phân tích & thiết kế – Ứng dụng thi TOEIC Reading (bài giữa kỳ)

Quy ước nhãn: **[PDF]** = yêu cầu bắt buộc trong đề · **[ĐX]** = giải pháp kỹ thuật đề xuất · **[GĐ]** = giả định cần xác minh.

## 0. Hiện trạng đã kiểm tra (thực tế, không suy đoán)

| Mục | Kết quả kiểm tra |
|---|---|
| PDF `Đề thi giữa kỳ v2.pdf` | Đã trích văn bản bằng `pdftotext` (113 dòng). **Đây là đề bài/yêu cầu, KHÔNG phải đề TOEIC gốc.** Chưa có nội dung câu hỏi/đáp án thật. Ảnh/sơ đồ trong PDF (nếu có) chưa xem được vì máy thiếu `pdftoppm`; mới đọc phần chữ. |
| Source backend | Đã có `backend/2380601390/10.2.0/` = template **ASP.NET Boilerplate (Module Zero) 10.2.0, ASP.NET Core MVC + jQuery**, `net9.0`, EF Core 9.0.5 + SqlServer. Solution `2380601390.sln` gồm Core, Application, EntityFrameworkCore, Migrator, Web.Core, Web.Host, Web.Mvc + 2 project test. Đây là ABP *Boilerplate*, không phải ABP Framework (abp.io). |
| Frontend | Thư mục `frontend/` **rỗng**. |
| Connection string | `Server=localhost; Database=2380601390Db; Trusted_Connection=True` – SQL Server 2022 trên máy đã kết nối được. |
| **Template chưa build được** | Đã chạy `dotnet build` project Core → lỗi CS1001/CS1002: tên project `2380601390` bắt đầu bằng chữ số nên `namespace 2380601390.…` không hợp lệ trong C#. Cần đổi tên (xem §12). |
| .NET runtime | Máy có SDK 10.0.401, runtime 8.0 và 10.0; **không có runtime 9.0** mà template nhắm tới (`net9.0`). Cần cài .NET 9 hoặc chuyển target (xem §12). |
| Figma / Stitch | Chưa có link. Không bịa link. |
| File Word đề | Chưa có. |

## 1. Bảng đối chiếu tiêu chí chấm điểm

| # | Tiêu chí [PDF] | Điểm | Đáp ứng bằng [ĐX] | Trạng thái |
|---|---|---|---|---|
| 1 | File WORD TOEIC có tags | 1 | Chuẩn tags v1 (§4) + file .docx mẫu đúng chuẩn, đủ Part 5/6/7 và ảnh | Chưa làm – cần nội dung đề từ giảng viên |
| 2 | Phân tích & thiết kế CSDL | 1 | ERD + ràng buộc (§3), phiên bản đề bất biến | Thiết kế xong, chưa cài đặt |
| 3 | Upload, tạo đề từ Word, kích hoạt ca thi | 1 | Import all-or-nothing (§5) + Publish (§7) | Chưa làm |
| 4 | Cập nhật/chỉnh sửa nội dung đề | 1 | Sửa trên bản Nháp, validate lại, xuất bản thành version mới | Chưa làm |
| 5 | Danh sách đề cho user chọn | 1 | API + màn hình mobile (tên, thời gian, số câu) | Chưa làm |
| 6 | Chức năng thi + link thiết kế | 3 | Đồng hồ server, autosave, hoán vị, nộp idempotent (§6, §8) + link Figma/Stitch | Chưa làm; link do bạn cung cấp |
| 7 | Hiển thị kết quả | 1 | Đúng/sai/bỏ qua + tỷ lệ (§9) | Chưa làm |
| 8 | Giao diện đẹp, thân thiện | 1 | Thiết kế trước (danh sách màn hình ở §10), hiện thực theo thiết kế | Chưa làm |

Sản phẩm nộp [PDF]: (1) file Word đề có tags; (2) file Word báo cáo theo mẫu (**mẫu chưa được cung cấp**); (3) source trên GitHub + video demo full screen lên Google Drive (bạn tự quay/upload); (4) link Figma/Stitch.

Yêu cầu chức năng [PDF] tóm tắt: giảng viên upload .docx → sai định dạng thì **hủy toàn bộ, không lưu**, hiện lỗi; xem trước (rà chính tả, ảnh); xuất bản; thống kê (số lượt làm, điểm TB, Top 10 câu sai nhiều nhất). Sinh viên: danh sách đề; làm bài với đếm ngược 75 phút, cảnh báo sắp hết giờ, tự thu bài, khóa chọn khi hết giờ; lưu tạm, đổi/hủy lựa chọn; cảnh báo số câu chưa làm trước khi nộp; chấm tự động (đúng/sai/bỏ qua). Tags phải định nghĩa được: nội dung + đáp án, đáp án đúng, gợi ý, hoán vị câu hỏi, hoán vị câu trả lời.

## 2. Kiến trúc [ĐX]

```
GiuaKy/
  backend/<template>/aspnet-core/src/
    *.Core                 Entity, Domain Service (Import parser, ShuffleService, ScoringService), Permission, Setting
    *.EntityFrameworkCore  DbContext (+DbSet), Fluent config, Migration, Seed (role Teacher/Student, user mẫu)
    *.Application          AppService + DTO (Teacher/ và Student/ tách riêng), AutoMapper profile
    *.Web.Core             Cấu hình JWT, filter chung
    *.Web.Host             API cho mobile (Swagger, JWT) – đã có trong template
    *.Web.Mvc              Website giảng viên: Controller + View + jQuery (Cookie auth của template)
  frontend/                Ứng dụng mobile-web sinh viên (gọi API Web.Host)
  docs/                    Tài liệu thiết kế, file Word mẫu
```

- Tuân thủ layering của ABP: Entity ở **Core**, `IRepository<T>`/`IDomainService`, AppService ở **Application** kế thừa `ApplicationService`, tự động lộ API qua dynamic Web API; Mvc controller gọi AppService. Không tạo kiến trúc khác.
- Đăng nhập/phân quyền: dùng **User/Role/Permission của Module Zero**. Thêm 2 role tĩnh `Teacher`, `Student` và permission `Pages.Exams.*` (Manage/Publish/Statistics) cho giảng viên, `Pages.Attempts` cho sinh viên. Không tạo bảng User riêng.
- Mobile gọi **Web.Host** (JWT `api/TokenAuth/Authenticate` có sẵn). Website giảng viên chạy trong Web.Mvc (cookie). Cả hai dùng chung tầng Application, nên không nhân đôi logic.
- Đọc .docx: **DocumentFormat.OpenXml** (Open XML SDK, MIT) – đọc được paragraph/run/drawing/relationship ảnh. Chọn phiên bản tương thích net9 khi cài (sẽ xác nhận lúc `dotnet add package`).
- Frontend: theo yêu cầu của bạn, **ứng dụng web có giao diện mobile (không dùng Flutter/Android Studio)**. [ĐX] Vite + vanilla JS/TS hoặc React nhẹ, viewport 360–430px, hiển thị trong khung điện thoại khi mở trên desktop. [GĐ] PDF nói "ứng dụng di động"; việc giảng viên chấp nhận mobile-web cần được xác nhận (bạn đã chủ động đề xuất, tôi ghi lại là giả định).
- Thiết kế UI: Figma hoặc Stitch. Tôi có công cụ Figma MCP nhưng chưa tạo gì; tạo file thiết kế cần bạn đồng ý/đăng nhập. Hiện chỉ lập danh sách màn hình (§10).

## 3. Thiết kế CSDL [ĐX]

### 3.1 ERD

```
Exam 1─* ExamVersion 1─* ExamPart 1─* ReadingGroup 1─* ReadingDocument 1─* ContentBlock ─? MediaAsset
                              │            │
                              │            └─* Question (GroupId, nullable)   ← Part 6/7
                              └──────────────* Question (GroupId = NULL)       ← Part 5
Question 1─* QuestionOption ;  Question.CorrectOptionId ─> QuestionOption
ExamVersion 1─* ExamAttempt *─1 AbpUsers
ExamAttempt 1─* AttemptGroup(thứ tự nhóm) ; 1─* AttemptQuestion(thứ tự + đáp án đã chọn) 1─* AttemptOption(thứ tự lựa chọn)
```

### 3.2 Bảng chính (PK `bigint identity`; entity kế thừa `Entity<long>`/`FullAuditedEntity<long>`)

| Bảng | Cột chính | Ràng buộc / index |
|---|---|---|
| **Exam** | Title, Code | UNIQUE(Code) |
| **ExamVersion** | ExamId, VersionNo, Status (`Draft`/`Published`/`Archived`), DurationMinutes, TotalQuestions, TagsVersion, PublishedAtUtc, SourceFileName, SourceFileSha256 | UNIQUE(ExamId, VersionNo); index(Status). **Bất biến khi Published.** |
| **ExamPart** | ExamVersionId, PartNo (5/6/7), FromNo, ToNo, ShuffleItems (bool), DisplayOrder | UNIQUE(ExamVersionId, PartNo) |
| **ReadingGroup** | ExamPartId, Code (id trong file), DisplayOrder, ShuffleQuestions (bool) | UNIQUE(ExamPartId, Code) |
| **ReadingDocument** | ReadingGroupId, Code, Title?, DisplayOrder | UNIQUE(GroupId, DisplayOrder) |
| **ContentBlock** | ReadingDocumentId, DisplayOrder, BlockType (`Text`/`Image`), Text (nvarchar(max), có đánh dấu chỗ trống `{{blank:Qxxx}}` nếu Part 6), MediaAssetId?, AltText | UNIQUE(DocumentId, DisplayOrder); CHECK: Text hoặc MediaAssetId đúng một cái non-null theo BlockType |
| **Question** | ExamVersionId, ExamPartId, ReadingGroupId?, Code (id ổn định), SourceNo (số câu gốc 101–200), GroupOrder, StemText?, Explanation?, ShuffleOptions (bool), CorrectOptionId? | UNIQUE(ExamVersionId, Code); UNIQUE(ExamVersionId, SourceNo); index(ReadingGroupId) |
| **QuestionOption** | QuestionId, Code (`A`–`D`, mã ổn định), Text, SourceOrder | UNIQUE(QuestionId, Code); mỗi câu đúng 4 dòng (kiểm ở import + khi publish); `Question.CorrectOptionId` FK → QuestionOption |
| **MediaAsset** | Sha256, ContentType, SizeBytes, Data (varbinary(max)) | UNIQUE(Sha256) – tái dùng ảnh trùng; đặt trong bảng riêng để các truy vấn khác không kéo blob |
| **ExamAttempt** | UserId (FK AbpUsers), ExamVersionId, Status (`InProgress`/`Submitted`/`AutoSubmitted`), StartedAtUtc, ExpiresAtUtc, SubmittedAtUtc?, CorrectCount?, WrongCount?, SkippedCount?, TotalQuestions, ScorePercent?, RowVersion | **Filtered UNIQUE(UserId, ExamVersionId) WHERE Status = InProgress** → không mở 2 lượt cùng lúc; index(ExamVersionId, Status); index(Status, ExpiresAtUtc) cho worker thu bài |
| **AttemptGroup** | AttemptId, ReadingGroupId, DisplayOrder | UNIQUE(AttemptId, ReadingGroupId), UNIQUE(AttemptId, DisplayOrder) |
| **AttemptQuestion** | AttemptId, QuestionId, DisplayOrder (số trình bày 1…100), SelectedOptionId?, AnswerRevision (int), AnsweredAtUtc?, IsCorrect? (ghi khi chấm) | UNIQUE(AttemptId, QuestionId), UNIQUE(AttemptId, DisplayOrder); index(QuestionId, IsCorrect) cho thống kê |
| **AttemptOption** | AttemptQuestionId, QuestionOptionId, DisplayOrder (0–3 → nhãn A–D) | UNIQUE(AttemptQuestionId, QuestionOptionId), UNIQUE(AttemptQuestionId, DisplayOrder) |

Gộp "AttemptAnswer" vào `AttemptQuestion` (SelectedOptionId + AnswerRevision) vì mỗi câu chỉ có một đáp án hiện hành → ít bảng, cập nhật nguyên tử bằng một câu UPDATE. Lịch sử thay đổi đáp án không phải yêu cầu của PDF nên không lưu.

### 3.3 Quy tắc dữ liệu
- **Ba khái niệm tách rời**: `Code` (id ổn định, do file tags đặt), `SourceNo` (số câu gốc, ví dụ 131), `DisplayOrder` (thứ tự trình bày của lượt thi). Chấm điểm và thống kê dựa trên `QuestionId`/`QuestionOptionId`, không dựa vào nhãn A/B/C/D hiển thị.
- **Part 5**: Question với `ReadingGroupId = NULL`. **Part 6/7**: Question bắt buộc có group. CHECK ở tầng ứng dụng + import (SQL không biểu diễn gọn được quan hệ chéo bảng).
- **Phiên bản bất biến**: ExamVersion `Published` không bị UPDATE/DELETE nội dung (AppService từ chối). Sửa đề đã xuất bản = nhân bản (clone) sang version Draft mới (VersionNo+1), sửa, validate, publish; version cũ chuyển `Archived` (không nhận lượt mới, lượt đang làm vẫn hoàn thành trên version cũ). Mọi FK của Attempt trỏ tới dòng thuộc version cũ nên lượt cũ không đổi.
- Ảnh: lưu trong DB (`MediaAsset.Data`) → **ghi ảnh cùng transaction với đề**, loại bỏ bài toán đồng bộ SQL–filesystem. Phục vụ qua endpoint có `ETag`/cache. Ghi chú: nếu bạn muốn lưu file ra đĩa thì dùng quy trình staging ở §5.4.
- Thống kê: xem §7.

## 4. Chuẩn tags Word – phiên bản `tagsv=1` [ĐX]

### 4.1 Quy tắc chung
1. **Một thẻ = một đoạn (paragraph) Word riêng**, nằm ở body của tài liệu. Cú pháp: `[TÊN thuộc_tính=giá_trị …]`. Tên thẻ viết hoa, không phân biệt hoa/thường khi đọc; thuộc tính viết thường.
2. Thẻ khối có thẻ đóng `[/TÊN]`: `EXAM`, `PART`, `GROUP`, `DOCUMENT`, `QUESTION`, `EXPLANATION`. Thẻ dòng không có thẻ đóng: `OPTION`, `ANSWER`.
3. **Xử lý Word tách thẻ thành nhiều run**: parser ghép toàn bộ `w:t` của mọi `w:r` trong cùng một `w:p` (kể cả `w:hyperlink`, `w:smartTag`), chuẩn hóa: NBSP → khoảng trắng, nháy cong `“ ” ‘ ’` → `"` `'`, gộp khoảng trắng, trim; rồi mới nhận dạng thẻ. Định dạng chữ (đậm/nghiêng) trong dòng thẻ bị bỏ qua.
4. **Thẻ chỉ hợp lệ ở body**; **thẻ nằm trong bảng (table), header/footer, textbox, footnote → lỗi `E_TAG_IN_TABLE`/`E_TAG_UNSUPPORTED_AREA`**. Bảng được phép xuất hiện như *nội dung* bên trong `DOCUMENT` (v1: hiển thị dạng khối bảng, làm sau lõi – xem §11 thứ tự).
5. Thuộc tính: `key=value`; giá trị có khoảng trắng đặt trong nháy kép. `on|off` cho cờ boolean. Thuộc tính lạ → lỗi (không bỏ qua âm thầm).
6. **Văn bản ngoài thẻ**: giữa `[EXAM]` và `[PART]` chỉ được trống; nội dung chỉ hợp lệ trong `DOCUMENT` (bài đọc), thân `QUESTION` (đề câu), `OPTION` (cùng dòng), `EXPLANATION`. Đoạn chữ lạc chỗ → `E_STRAY_TEXT` (không đoán).
7. **Ảnh**: ảnh *nhúng* trong file .docx (inline `w:drawing`/`pic:pic` có `r:embed`), đặt **trong paragraph riêng** nằm giữa `[DOCUMENT]` … `[/DOCUMENT]` đúng vị trí muốn hiển thị. Thứ tự ảnh/chữ = thứ tự paragraph trong tài liệu. Ảnh được trích từ package .docx (không dùng đường dẫn ngoài). Ảnh *floating/anchor* hoặc ảnh link ngoài (`r:link`) → lỗi `E_IMAGE_UNSUPPORTED`. Ảnh ngoài `DOCUMENT` (ví dụ trong QUESTION/OPTION) v1 → lỗi (chưa hỗ trợ). Chú thích ảnh: dùng "Alt text" của Word (`docPr/@descr`) – không bắt buộc.
8. Mã/ID: `[A-Za-z0-9_-]{1,40}`, duy nhất trong phạm vi đề.

### 4.2 Từ vựng thẻ

```
[EXAM id=<id> tagsv=1 title="<tên>" duration=<phút> total=<số câu>]      (1 lần, mở đầu)
  [PART no=5|6|7 from=<n> to=<m> shuffle-items=on|off]                   (mỗi part đúng 1 lần)
     -- Part 5 --
     [QUESTION id=<id> no=<số câu gốc> shuffle-options=on|off]
        (đoạn văn: đề câu)
        [OPTION id=A]<nội dung>      … 4 dòng, id = A,B,C,D
        [ANSWER ref=<id option>]
        [EXPLANATION] … [/EXPLANATION]
     [/QUESTION]
     -- Part 6, 7 --
     [GROUP id=<id> shuffle-questions=on|off]                            (shuffle-questions mặc định off nếu bỏ qua)
        [DOCUMENT id=<id>]  … đoạn chữ / ảnh theo thứ tự … [/DOCUMENT]   (≥1 DOCUMENT)
        [QUESTION …] … [/QUESTION]                                       (≥1 câu)
     [/GROUP]
  [/PART]
[/EXAM]
```

- `shuffle-items` ở PART: Part 5 → hoán vị *câu độc lập*; Part 6/7 → hoán vị *nguyên nhóm*.
- `shuffle-questions` ở GROUP: hoán vị câu *trong* nhóm. **Part 6: bắt buộc `off`** (nếu `on` → `E_SHUFFLE_FORBIDDEN`). Part 7: mặc định off, cho phép `on` khi giảng viên chủ động.
- `shuffle-options` ở QUESTION: **bắt buộc khai báo** (không có mặc định ngầm) để file tự mô tả. Nếu đáp án kiểu "Both A and B" hoặc phụ thuộc thứ tự → đặt `off`.
- `no` = số câu gốc; trong từng PART phải liên tục tăng đúng theo thứ tự xuất hiện và nằm trong `[from, to]`. `total` phải bằng tổng số câu.
- Part 6 – **chỗ trống**: trong `DOCUMENT` dùng đánh dấu nội tuyến `{{blank:<id câu>}}`, ví dụ `{{blank:P6-131}}`. Mỗi `QUESTION` của nhóm Part 6 phải được đúng một `{{blank:…}}` tham chiếu, và mỗi `{{blank}}` phải trỏ đúng một câu trong cùng nhóm. Thân câu hỏi Part 6 có thể rỗng (chỉ có `no` + 4 `OPTION`).
- `ANSWER ref=` tham chiếu **mã option ổn định** (A–D), không phải vị trí hiển thị.
- `EXPLANATION` **bắt buộc** (PDF yêu cầu gợi ý trả lời). Chỉ hiển thị cho sinh viên sau khi nộp (§9).
- [GĐ] Dải số câu mặc định theo TOEIC thực: Part 5 = 101–130, Part 6 = 131–146, Part 7 = 147–200, tổng 100. Dải khai báo trong file (`from`/`to`) nên không hard-code; nếu giảng viên dùng đánh số 1–100 vẫn khai báo được.

### 4.3 Ví dụ hợp lệ (nội dung MINH HỌA tự soạn, không phải đề TOEIC thật)

```
[EXAM id=DEMO-01 tagsv=1 title="Demo Reading Test 01 (minh hoa)" duration=75 total=100]
[PART no=5 from=101 to=130 shuffle-items=on]
[QUESTION id=P5-101 no=101 shuffle-options=on]
The manager asked all staff to ------- the report before Friday.
[OPTION id=A]submit
[OPTION id=B]submits
[OPTION id=C]submitting
[OPTION id=D]submission
[ANSWER ref=A]
[EXPLANATION]
Sau "to" cần động từ nguyên mẫu: submit.
[/EXPLANATION]
[/QUESTION]
… (câu 102–130) …
[/PART]

[PART no=6 from=131 to=146 shuffle-items=on]
[GROUP id=G6-1 shuffle-questions=off]
[DOCUMENT id=D6-1]
Dear residents, the lobby will be closed for {{blank:P6-131}} on May 3.
Please use the side entrance. {{blank:P6-132}} We apologize for the inconvenience.
[/DOCUMENT]
[QUESTION id=P6-131 no=131 shuffle-options=on]
[OPTION id=A]repair
[OPTION id=B]repairs
[OPTION id=C]repairing
[OPTION id=D]repaired
[ANSWER ref=B]
[EXPLANATION]
Sau giới từ "for" cần danh từ; "repairs" phù hợp ngữ cảnh.
[/EXPLANATION]
[/QUESTION]
…
[/GROUP]
[/PART]
```
Ghi chú: `[EXPLANATION]` và `[/EXPLANATION]` luôn ở paragraph riêng, nội dung nằm ở các paragraph giữa hai thẻ.

```
[PART no=7 from=147 to=200 shuffle-items=on]
[GROUP id=G7-5 shuffle-questions=off]
[DOCUMENT id=D7-5a]
Email – From: A. Nguyen … (đoạn chữ)
[paragraph chứa ảnh nhúng: logo công ty]
Tiếp tục đoạn chữ …
[/DOCUMENT]
[DOCUMENT id=D7-5b]
Invoice (ảnh nhúng bảng giá) …
[/DOCUMENT]
[QUESTION id=P7-186 no=186 shuffle-options=on]
What is the purpose of the email?
[OPTION id=A]…
[OPTION id=B]…
[OPTION id=C]…
[OPTION id=D]…
[ANSWER ref=C]
[EXPLANATION]…[/EXPLANATION]
[/QUESTION]
… (các câu 187–190) …
[/GROUP]
[/PART]
[/EXAM]
```
(Các dấu `…` chỉ là lược bớt trong tài liệu này; file Word thật phải viết đầy đủ.)

### 4.4 Ví dụ lỗi → thông báo
| Lỗi trong file | Mã | Thông báo hiển thị cho giảng viên |
|---|---|---|
| Thiếu `[/GROUP]` | `E_TAG_UNCLOSED` | "Đoạn 412: thẻ [GROUP id=G6-1] mở ở đoạn 389 chưa được đóng trước [/PART]." |
| Câu chỉ có 3 OPTION | `E_OPTION_COUNT` | "Part 5, câu 107 (id P5-107, đoạn 151): có 3 lựa chọn, yêu cầu đúng 4 (A–D)." |
| `ANSWER ref=E` | `E_ANSWER_NOT_FOUND` | "Câu 113 (đoạn 190): đáp án 'E' không nằm trong A–D." |
| Hai câu cùng `no=118` | `E_DUP_NO` | "Câu 118 trùng (đoạn 205 và đoạn 214)." |
| Sau câu 120 là câu 122 | `E_NO_SEQUENCE` | "Part 5: sau câu 120 phải là 121 nhưng gặp 122 (đoạn 231)." |
| Thiếu EXPLANATION | `E_EXPLANATION_MISSING` | "Câu 125: thiếu [EXPLANATION]." |
| `{{blank:P6-139}}` không có câu | `E_BLANK_REF` | "Group G6-3, đoạn 301: chỗ trống tham chiếu câu 'P6-139' không có trong nhóm." |
| Câu Part 6 không có chỗ trống | `E_BLANK_MISSING` | "Group G6-2: câu 134 không được đánh dấu {{blank}} trong bài đọc." |
| Thẻ trong bảng | `E_TAG_IN_TABLE` | "Bảng 3, hàng 1, ô 2: không được đặt thẻ [OPTION] trong bảng." |
| Ảnh floating / ngoài tài liệu | `E_IMAGE_UNSUPPORTED` | "Đoạn 355: ảnh không phải dạng 'In line with text' hoặc không nhúng trong file." |
| Part 6 `shuffle-questions=on` | `E_SHUFFLE_FORBIDDEN` | "Group G6-1: Part 6 không cho hoán vị câu trong nhóm." |
| Tổng câu ≠ total | `E_TOTAL_MISMATCH` | "Khai báo total=100 nhưng đếm được 98 câu." |
| Văn bản lạc | `E_STRAY_TEXT` | "Đoạn 17: nội dung nằm ngoài thẻ hợp lệ." |
| Tệp không phải .docx / hỏng | `E_FILE_INVALID` | "Tệp không phải .docx hợp lệ hoặc không đọc được." |

Mọi lỗi trả về dạng `{code, message, paragraphIndex|tableIndex, partNo, groupCode, questionNo, tag}`. Lỗi được gom theo **hai giai đoạn** (đã cài đặt): (1) đọc file + cú pháp/cấu trúc thẻ + ảnh; (2) chỉ khi giai đoạn 1 sạch mới kiểm ngữ nghĩa (số câu, đáp án, chỗ trống...). Cách này tránh lỗi dây chuyền do thiếu/đóng sai thẻ; trong mỗi giai đoạn mọi lỗi được báo cùng lúc, không dừng ở lỗi đầu tiên.

## 5. Quy trình import (all-or-nothing) [ĐX]

1. **Upload**: Mvc nhận `.docx` (giới hạn dung lượng, kiểm tra phần mở rộng + chữ ký ZIP `PK`, mở bằng OpenXML để bắt file hỏng). Chỉ giữ trong bộ nhớ/`MemoryStream`; không ghi đĩa.
2. **Đọc** → tuần tự hóa body thành danh sách *item* (paragraph/bảng/ảnh) có chỉ số đoạn, đã ghép run (§4.1.3).
3. **Parse** (máy trạng thái có stack mở/đóng) → **mô hình trung gian** `ParsedExam` (POCO trong bộ nhớ, ảnh dạng `byte[]` + SHA-256), không chạm DB.
4. **Validate toàn bộ** (xem checklist PDF + brief): định dạng, thẻ đủ/đóng-mở đúng, trùng id/no, dải và thứ tự số câu, đủ `total`, mỗi câu đúng 4 option, đúng một đáp án tồn tại, có EXPLANATION, quan hệ Part→Group→Document→Question, chỗ trống Part 6, ảnh đọc được và gắn đúng DOCUMENT. Gom toàn bộ lỗi.
5. Có lỗi → trả danh sách lỗi (HTTP 400/trang kết quả), **không INSERT gì**, không để lại file tạm (không ghi đĩa ở bước 1).
6. Không lỗi → **một transaction** (`UnitOfWork`): Exam/ExamVersion(Draft) → Part → Group → Document → MediaAsset (dedupe SHA-256) → ContentBlock → Question → Option → set CorrectOptionId. Lỗi giữa chừng → rollback toàn bộ.
7. Bản mới ở trạng thái **Draft**; giảng viên xem trước, sửa, rồi Publish.

**§5.4 Nếu lưu ảnh ra filesystem (phương án thay thế)**: SQL transaction không rollback được file. Quy trình: (a) ghi ảnh vào `media/staging/<importId>/` trước khi mở transaction; (b) commit DB với đường dẫn `final`; (c) *sau commit* mới `Move` staging → final (cùng volume, rename nguyên tử), nếu move lỗi thì đánh dấu version `ImportFailed` và xóa dòng; (d) nếu validate/DB lỗi thì xóa thư mục staging trong `finally`; (e) `BackgroundWorker` quét staging cũ hơn N giờ và final không còn dòng tham chiếu để xóa. Tôi đề xuất không dùng phương án này mà lưu ảnh trong DB.

## 6. Hoán vị an toàn [ĐX]

Chạy **một lần khi bắt đầu lượt thi**, kết quả ghi vào `AttemptGroup/AttemptQuestion/AttemptOption`; mở lại bài đọc từ các bảng này nên thứ tự giữ nguyên.

Thuật toán (seed = `RandomNumberGenerator`, Fisher–Yates; seed không cần lưu vì kết quả đã lưu):
1. Duyệt các Part theo thứ tự cố định 5 → 6 → 7 (**ranh giới Part không bao giờ bị phá**).
2. Part 5: nếu `shuffle-items=on` trộn danh sách câu; ngược lại giữ thứ tự.
3. Part 6/7: danh sách *nhóm* – nếu `shuffle-items=on` trộn nguyên nhóm; trong mỗi nhóm giữ nguyên thứ tự DOCUMENT; trộn câu trong nhóm chỉ khi `shuffle-questions=on` (Part 6 luôn off).
4. Với mỗi câu: nếu `shuffle-options=on` trộn 4 option; ngược lại giữ A–D.
5. Gán `DisplayOrder` toàn cục 1…100 theo thứ tự trình bày; `SourceNo` giữ nguyên để tra cứu.
6. Chấm theo `SelectedOptionId == Question.CorrectOptionId` (id lựa chọn). Nhãn A/B/C/D hiển thị = `AttemptOption.DisplayOrder`.

Câu không bao giờ tách khỏi nhóm vì câu của nhóm luôn được xếp liền trong block của nhóm đó.

## 7. Giảng viên: trạng thái, xuất bản, thống kê [ĐX]

- **Draft**: mới import hoặc bản sao để sửa; sinh viên không thấy; sửa tự do. **Published**: bất biến, sinh viên thấy trong danh sách và được bắt đầu thi. **Archived**: bản cũ, không nhận lượt mới.
- "Kích hoạt ca thi" tối thiểu = nút **Xuất bản/Bắt đầu cho phép thi** (Draft → Published, sau khi chạy lại toàn bộ validate) và **Dừng nhận thi** (Published → Archived). Không có lịch, giờ mở/đóng, phòng thi (không mở rộng khi chưa cần).
- Sửa nội dung (text, option, đáp án, giải thích, cờ hoán vị): chỉ trên Draft; sau mỗi lần lưu chạy lại validator (4 option, 1 đáp án tồn tại, có giải thích, blank Part 6, …). Version Published muốn sửa → "Tạo bản sửa" (clone → Draft).
- **Thống kê** (theo ExamVersion, có bộ lọc chọn "tất cả version của Exam"):
  - Lượt được tính: chỉ `Status IN (Submitted, AutoSubmitted)`. Lượt `InProgress` hiển thị tách riêng ("đang làm: n"), không vào điểm trung bình hay Top 10.
  - Điểm TB = trung bình `ScorePercent` (= đúng/tổng ×100) của các lượt đã chấm.
  - Top 10 câu sai: xếp theo **số lượt sai** (chọn một đáp án khác đáp án đúng); cột phụ: số lượt bỏ qua và tỷ lệ sai = sai/(đã làm). **Bỏ qua không tính là sai.** [GĐ] PDF nói "nhiều sinh viên làm sai nhất" → chọn số lượt/sinh viên sai; đồng hạng xếp theo tỷ lệ sai giảm dần. Có thể đổi sang tỷ lệ nếu giảng viên yêu cầu.
  - Nhiều lần làm: [GĐ] mỗi sinh viên được làm lại; mọi lượt đã chấm đều được đếm (hiển thị thêm số sinh viên khác nhau). Nhiều version: thống kê mỗi version riêng vì `QuestionId` khác nhau; chế độ gộp chỉ gộp số liệu cấp đề (số lượt, điểm TB), không gộp theo câu.

## 8. Sinh viên: thi, thời gian, lưu đáp án, nộp [ĐX]

- Danh sách đề: chỉ `Published`; trả `{examVersionId, title, durationMinutes, totalQuestions, inProgressAttemptId?}`.
- **Bắt đầu/tiếp tục** (`POST /exams/{id}/attempts`): nếu đã có lượt InProgress còn hạn → trả lại lượt đó (idempotent); nếu có lượt hết hạn → finalize rồi tạo mới/hiện kết quả. Tạo lượt mới: `StartedAtUtc = UtcNow` (server), `ExpiresAtUtc = Started + Duration`, sinh hoán vị (§6), một transaction.
- **Đồng hồ**: mọi response trả `serverNowUtc`, `expiresAtUtc`, `remainingSeconds`. Client tính `offset = serverNow − Date.now()` lúc nhận và dùng `performance.now()` (đơn điệu) để đếm; đổi giờ thiết bị không đổi hạn; reload → gọi lại API lấy mốc server. Cảnh báo ở 10 và 1 phút (cấu hình). Hết giờ → client khóa nhập và gọi nộp; **hiệu lực thực sự ở server**.
- **Hết hạn khi client đã đóng**: lazy finalize (mọi truy vấn lượt thi kiểm tra `now > ExpiresAt` → finalize) **và** `PeriodicBackgroundWorkerBase` của ABP quét `Status=InProgress AND ExpiresAtUtc < now` mỗi ~30s để chấm và đóng (`AutoSubmitted`).
- **Lưu đáp án**: `PUT /attempts/{id}/answers/{questionId}` `{optionId|null, revision}`. Client giữ bộ đếm `revision` tăng dần theo từng câu; server chỉ ghi nếu `revision > AnswerRevision` bằng một lệnh `UPDATE … WHERE Id=@id AND AnswerRevision < @rev AND attempt InProgress AND now ≤ ExpiresAt` → yêu cầu đến muộn/sai thứ tự không ghi đè lựa chọn mới. `optionId=null` = hủy lựa chọn. Server kiểm tra option thuộc đúng `AttemptOption` của câu đó. Response trả `{saved:true, revision}`.
- **Trạng thái đồng bộ ở UI**: mỗi câu có `saving / saved / error(retry)`; hàng đợi cục bộ (localStorage) chỉ là bộ đệm, **chỉ coi là đã lưu khi server trả OK**. Trước khi nộp: đợi hàng đợi rỗng tối đa ~5s rồi mới gọi submit; còn câu lỗi → cho người dùng thử lại hoặc nộp kèm cảnh báo.
- **Chính sách sau hạn**: [GĐ] server từ chối mọi ghi đáp án khi `now > ExpiresAt` (mã `ATTEMPT_EXPIRED`), không có thời gian ân hạn mặc định (tham số cấu hình, mặc định 0). Câu đã được lưu trước hạn vẫn được chấm.
- **Nộp bài** `POST /attempts/{id}/submit`: idempotent. Chuyển trạng thái bằng `UPDATE … SET Status=Submitted WHERE Id=@id AND Status=InProgress` (kèm RowVersion); chỉ request thắng mới chấm, request còn lại (nộp tay trùng tự thu bài, bấm 2 lần) đọc và trả về cùng kết quả. Sau khi nộp mọi `PUT answers` bị từ chối. Mọi API kiểm tra `attempt.UserId == CurrentUser`, nếu không → 404/403.

## 9. Chấm điểm & bảo mật [ĐX]

- Chấm ở server theo `ExamVersionId` cố định của lượt: `Correct`, `Wrong` (có chọn nhưng sai), `Skipped` (null). Kiểm tra bất biến `Correct + Wrong + Skipped = TotalQuestions` trước khi lưu.
- Điểm: `ScorePercent = Correct / Total × 100` (công bố trên UI). **Không quy đổi thang 495/990** vì chưa có bảng quy đổi được xác nhận.
- API làm bài dùng DTO riêng cho sinh viên **không chứa** `CorrectOptionId`, `Explanation`, `IsCorrect`. Test tự động kiểm tra JSON không có các trường này.
- Sau khi nộp, kết quả hiển thị: số đúng/sai/bỏ qua, tỷ lệ, danh sách từng câu với đáp án đã chọn, đáp án đúng và giải thích. [GĐ] Việc lộ đáp án đúng + giải thích sau khi nộp là chính sách tôi đề xuất (PDF chỉ nói "tính số câu đúng, sai, bỏ qua" và cần gợi ý); nếu muốn chặt hơn có thể chỉ hiện giải thích sau khi đề đóng.
- Phân quyền: role Teacher/Student (Module Zero); sinh viên không gọi được API `Exam*` của giảng viên (`[AbpAuthorize(PermissionNames.Pages_Exams_*)]`). Ảnh của đề chỉ phục vụ với đề Published đối với sinh viên.

## 10. API và giao diện [ĐX]

### 10.1 API (tiền tố `/api/services/app/…` do ABP tự sinh; dưới đây là tên logic). Lỗi chung: 401 chưa đăng nhập, 403 thiếu quyền, 404 không tồn tại/không thuộc về mình, 400 dữ liệu sai.

| API | Quyền | Request → Response | Lỗi chính |
|---|---|---|---|
| `POST ExamImport/Import` (multipart .docx) | Teacher | file → `{examVersionId}` hoặc `{errors:[{code,message,paragraph,part,group,question,tag}]}` | 400 `IMPORT_INVALID` (kèm danh sách lỗi), 413 file quá lớn |
| `GET Exam/GetAll` | Teacher | filter/paging → `ExamListItemDto[]` (title, version, status, số câu) | |
| `GET Exam/GetPreview?versionId` | Teacher | → cây đầy đủ Part/Group/Document/Block/Question/Option/đáp án/giải thích | 404 |
| `PUT Question/Update`, `PUT Group/…`, `PUT ContentBlock/…` | Teacher | trường được sửa → DTO mới + `validation:[…]` | 409 nếu version Published |
| `POST ExamVersion/CreateDraftFrom` | Teacher | `{versionId}` → draft mới | 409 nếu đã có draft |
| `POST ExamVersion/Publish`, `/Unpublish` | Teacher | `{versionId}` → trạng thái; Publish chạy lại validate | 400 `VALIDATION_FAILED` |
| `GET Statistics/GetByExam?examId` | Teacher | → `{finishedCount, inProgressCount, avgPercent, top10Wrong:[{questionNo,text,wrongCount,skippedCount,wrongRate}]}` | |
| `GET Media/{id}` | Teacher/Student* | ảnh (cache, ETag) | 404; *SV chỉ với đề Published |
| `GET StudentExam/GetPublished` | Student | → `[{id,title,durationMinutes,totalQuestions,inProgressAttemptId}]` | |
| `POST Attempt/Start` | Student | `{examVersionId}` → AttemptDto (đề theo thứ tự đã trộn, **không có đáp án**, kèm `serverNowUtc, expiresAtUtc`) | 404 đề chưa Published |
| `GET Attempt/Get?id` | Student(chủ) | như trên + đáp án đã chọn + trạng thái | 404/403; trả kết quả nếu đã nộp |
| `PUT Attempt/SaveAnswer` | Student(chủ) | `{attemptId,questionId,optionId|null,revision}` → `{saved,revision,remainingSeconds}` | 409 `ATTEMPT_SUBMITTED`, 410 `ATTEMPT_EXPIRED`, 400 option không thuộc câu |
| `POST Attempt/Submit` | Student(chủ) | `{attemptId}` → `ResultSummaryDto` | idempotent |
| `GET Attempt/GetResult?id` | Student(chủ) | → tổng hợp + từng câu có đáp án đúng/giải thích | 409 nếu chưa nộp |

DTO tách thư mục: `Application/Exams/Teacher/Dto/*` và `Application/Exams/Student/Dto/*`; DTO sinh viên không tái dùng class của giảng viên.

### 10.2 Màn hình cần thiết kế (để vẽ Figma/Stitch – **chưa vẽ**)
- **Website giảng viên**: Đăng nhập · Danh sách đề (trạng thái, nút Xuất bản) · Upload Word + bảng lỗi import (đoạn/nhóm/câu/tag, bấm hiện vị trí) · Xem trước (cột trái điều hướng Part/nhóm, nội dung bài đọc có ảnh, câu hỏi, đáp án đúng đánh dấu) · Sửa câu (modal) + banner lỗi validate · Thống kê (số lượt, điểm TB, Top 10 sai).
- **Mobile sinh viên**: Đăng nhập · Danh sách đề (tên, 75 phút, 100 câu) · Chi tiết/Bắt đầu hoặc Tiếp tục · Làm bài (thanh trên: đồng hồ + chỉ báo lưu; bài đọc có thể thu/gập + zoom ảnh; 4 lựa chọn lớn, bấm lại để hủy; Prev/Next; lưới điều hướng câu đã/chưa chọn) · Hộp thoại xác nhận nộp (số câu bỏ trống) · Màn hình hết giờ · Kết quả (đúng/sai/bỏ qua, % + danh sách câu xem giải thích).

## 11. Kiểm thử theo rủi ro (kế hoạch; chưa chạy)
Unit test parser/validator: file hợp lệ đủ 3 Part + ảnh; thiếu tag; thiếu option; trùng số câu; đáp án không tồn tại; lỗi cuối file không lưu gì (đếm bản ghi = 0 sau import lỗi); run bị tách. Test hoán vị: nhóm không bị tách, Part không đảo; chấm đúng sau khi trộn option. Test thi: reload không reset giờ, ghi sau hạn bị từ chối, nộp lặp/song song (2 request cùng lúc cho 1 kết quả), hủy chọn → bỏ qua, revision cũ không ghi đè, sửa đề không đổi lượt cũ, API sinh viên không chứa đáp án, sinh viên khác không xem được lượt của người.

## 12. Giả định, thiếu sót và thứ tự triển khai

**Chặn triển khai (cần bạn quyết)**
1. Đổi tên root namespace hợp lệ (đề xuất `ToeicReading`) thay cho `2380601390`, giữ nguyên cấu trúc project của template. Nếu giảng viên bắt buộc dùng MSSV làm tên project, phương án là tiền tố hợp lệ như `_2380601390` – cần xác nhận.
2. Cài .NET 9 runtime/SDK (khuyến nghị, đúng `net9.0` của template) **hoặc** retarget sang net10 (phải kiểm tra tương thích Abp 10.2.0 + EF Core 9 – rủi ro).
3. **Nội dung đề TOEIC thật**: cần PDF đề thực tế do giảng viên cung cấp (file bạn gửi chỉ là đề bài). Nếu chưa có tôi chỉ dựng đề minh họa có gắn nhãn "dữ liệu minh họa" để chạy thử, không phải đề thật/đáp án thật.
4. Link Figma/Stitch và mẫu báo cáo Word: chưa có.

**Giả định cần xác minh**: mobile-web chấp nhận được; dải câu 101–200; tính mọi lượt đã chấm trong thống kê; Top 10 theo số lượt sai; không ân hạn khi hết giờ; hiện đáp án + giải thích ngay sau khi nộp; sinh viên được làm lại; mỗi sinh viên chỉ một lượt InProgress/đề; điểm = % đúng; ảnh lưu trong DB.

**Thứ tự triển khai (mỗi bước chạy và kiểm được)**
0. Sửa tên + build được template, tạo DB, đăng nhập (kiểm: `dotnet build`, `dotnet ef database update`/Migrator, chạy Web.Mvc).
1. Entity + migration + seed role Teacher/Student/tài khoản mẫu.
2. Parser + validator + unit test (tập file Word đúng/sai) – chưa cần UI.
3. Import AppService + trang upload/lỗi (Web.Mvc) + preview.
4. Danh sách đề, sửa nội dung, Publish/Archived, clone Draft.
5. Student API: start/get/save/submit + hoán vị + worker tự thu + test đồng thời.
6. Frontend mobile-web theo thiết kế: danh sách → làm bài → kết quả.
7. Thống kê + Top 10.
8. File Word mẫu đúng chuẩn, báo cáo, hoàn thiện UI.

## 13. Link thiết kế giao diện (Figma)

https://www.figma.com/design/m4n5N1bL0pMEs0KeoUsEtQ

- Trang "1. Web Giảng viên": W1–W5 (danh sách đề + upload, lỗi import, xem trước, sửa câu hỏi, thống kê).
- Trang "2. Mobile Sinh viên": M1–M10 (danh sách đề, bắt đầu, làm bài Part 5/7, lưới câu, sắp hết giờ + lưu lỗi, xác nhận nộp, hết giờ, kết quả, xem lại).
- Không có màn hình đăng ký/đăng nhập theo yêu cầu. Dữ liệu trong thiết kế là minh họa.
