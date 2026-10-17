# TOEIC Reading – Bài giữa kỳ

Ứng dụng thi TOEIC Reading: **website giảng viên** (ASP.NET Boilerplate MVC + jQuery, SQL Server) và **ứng dụng sinh viên dạng mobile-web** (gọi API của backend).

## Thiết kế giao diện (Figma)

**https://www.figma.com/design/m4n5N1bL0pMEs0KeoUsEtQ**

| Trang trong file Figma | Nội dung |
|---|---|
| 1. Web Giảng viên (1440 px) | W1 Danh sách đề + upload · W2 Upload có lỗi · W3 Xem trước đề · W4 Sửa câu hỏi · W5 Thống kê & Top 10 câu sai |
| 2. Mobile Sinh viên (390×844) | M1 Danh sách đề · M2 Bắt đầu · M3/M4 Làm bài Part 5 / Part 7 · M5 Lưới câu hỏi · M6 Sắp hết giờ & lưu lỗi · M7 Xác nhận nộp · M8 Hết giờ · M9 Kết quả · M10 Xem lại đáp án |

Theo yêu cầu, thiết kế **không có** màn hình đăng ký/đăng nhập. Dữ liệu trong thiết kế là dữ liệu minh họa.

## Cấu trúc thư mục

```
GiuaKy/
  backend/2380601390/10.2.0/aspnet-core/   ASP.NET Boilerplate 10.2.0 (net9.0): Core, Application, EntityFrameworkCore, Web.Core, Web.Host (API), Web.Mvc (web giảng viên)
  frontend/                                 Ứng dụng sinh viên (mobile-web)
  docs/PHAN_TICH_THIET_KE.md                Phân tích yêu cầu, ERD, chuẩn tags Word, API, kế hoạch
```

Namespace C# dùng tiền tố `_2380601390` (tên project bắt đầu bằng số không hợp lệ trong C#); tên thư mục/solution giữ nguyên MSSV.

## Chạy backend

Yêu cầu: .NET 9 SDK/runtime, SQL Server (mặc định `localhost`, Windows Authentication, DB `2380601390Db`), Node.js.

```bash
cd backend/2380601390/10.2.0/aspnet-core
dotnet build 2380601390.sln

# tạo/cập nhật CSDL (migration + dữ liệu mẫu)
dotnet run --project src/2380601390.Migrator -- -q

# tải thư viện giao diện cho website giảng viên (lần đầu)
cd src/2380601390.Web.Mvc
npm install --ignore-scripts
npx gulp buildDev        # tạo bundle; thoát bằng Ctrl+C sau khi hiện "Bundles are being created"

# chạy website giảng viên
dotnet run --urls http://localhost:5199
```

Tài khoản demo (dữ liệu seed, mật khẩu `123qwe`): `giangvien` (role Teacher), `sinhvien` (role Student), `admin`.

API cho mobile: chạy `dotnet run --project src/2380601390.Web.Host` (http://localhost:44311, Swagger tại `/swagger`), đăng nhập bằng `POST /api/TokenAuth/Authenticate` (`{"userNameOrEmailAddress","password"}`) rồi gửi `Authorization: Bearer <accessToken>`. CORS đã mở cho `localhost:5173/5174/3000`.

API sinh viên (role Student), tiền tố `/api/services/app/StudentExam`:

| Method | Endpoint | Mô tả |
|---|---|---|
| GET | `/GetPublished` | Đề đã xuất bản: tên, thời gian, số câu, lượt đang làm dở, điểm lần gần nhất |
| POST | `/Start` `{examVersionId}` | Bắt đầu hoặc tiếp tục (cùng lượt, cùng thứ tự đã trộn, cùng hạn); trả đề không có đáp án + `expiresAtUtc`, `serverNowUtc`, `remainingSeconds` |
| GET | `/Get?AttemptId=` | Mở lại lượt thi: khôi phục đáp án đã chọn |
| POST | `/SaveAnswer` `{attemptId, questionId, optionId\|null, revision}` | Lưu/hủy đáp án; `revision` cũ hơn bản đã lưu bị bỏ qua (`saved:false`); sau hạn trả lỗi 4101 |
| POST | `/Submit` `{attemptId}` | Nộp (idempotent) và chấm |
| GET | `/GetResult?AttemptId=` | Kết quả + đáp án đúng + giải thích (chỉ sau khi nộp) |
| GET | `/GetMyResults` | Lịch sử các lượt đã nộp |
| GET | `/api/media/{sha256}` | Ảnh của bài đọc (đề không phải Nháp) |

Mã lỗi nghiệp vụ (trường `error.code`): 4101 hết giờ · 4102 đã nộp · 4104 đề chưa xuất bản · 4105 lựa chọn không thuộc câu · 4106 chưa nộp nên chưa xem kết quả. Lượt hết hạn được tự thu bài bởi background worker (~30 giây/lần) kể cả khi client đã đóng.

## Trạng thái

- Xong: import Word theo chuẩn tags v1 (all-or-nothing), xem trước, sửa bản Nháp, xuất bản / dừng / tạo bản sửa, phiên bản đề bất biến, API sinh viên (danh sách, bắt đầu/tiếp tục, hoán vị, lưu đáp án, nộp, chấm, kết quả, tự thu bài hết giờ).
- Xong: frontend mobile-web (`frontend/`, Vite) hiện thực theo thiết kế Figma, đọc thiết kế qua Figma MCP; chạy: `cd frontend && npm install && npm run dev` (http://localhost:5173, cần chạy Web.Host trước).
- Xong: thống kê cho giảng viên (menu "Thống kê", `/Exams/Statistics`): số lượt đã nộp, đang làm, điểm TB, tự thu bài và Top 10 câu sai theo W5. Chỉ tính lượt đã nộp; Top 10 xếp theo số lượt sai (bỏ qua không tính là sai); thống kê theo từng phiên bản đề.
- Xong: file Word đề mẫu đúng chuẩn tags: `docs/sample/TOEIC_Reading_Test05.docx` (100 câu, 3 Part, 4 ảnh nhúng; nội dung lấy từ `Reading_v05.docx`, đã sửa lỗi OCR). Đáp án + giải thích: `docs/sample/DAP_AN_Test05.md`. **Đáp án do người soạn tự xác định (file nguồn không có đáp án) – cần đối chiếu đáp án chính thức.** Tạo lại bằng `python docs/sample/build_sample.py` (ảnh: `make_images.ps1`).
- Chưa xong: file báo cáo theo mẫu (chưa có mẫu).
- Test: `dotnet test backend/2380601390/10.2.0/aspnet-core/test/2380601390.Tests` (phần API sinh viên chưa có test).
