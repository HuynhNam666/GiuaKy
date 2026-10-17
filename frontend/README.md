# Frontend – ứng dụng sinh viên (mobile-web)

Giao diện mobile chạy trên web (khung 390×844 khi mở trên máy tính, toàn màn hình khi mở trên điện thoại), hiện thực **theo thiết kế Figma**:
https://www.figma.com/design/m4n5N1bL0pMEs0KeoUsEtQ (trang "2. Mobile Sinh viên", màn M1–M10).

Màu sắc, kích thước, bo góc, font (Inter) và các icon (`public/icons/*.svg`) được lấy trực tiếp từ file Figma qua Figma MCP (`get_design_context`).

## Chạy

```bash
# 1. Backend API (Web.Host): http://localhost:44311
cd ../backend/2380601390/10.2.0/aspnet-core/src/2380601390.Web.Host && dotnet run

# 2. Frontend
cd frontend
npm install
npm run dev          # http://localhost:5173
```

Cấu hình trong `.env` (xem `.env.example`): `VITE_API_URL`, `VITE_STUDENT_USER`, `VITE_STUDENT_PASSWORD`.
Theo yêu cầu **không có màn hình đăng ký/đăng nhập**: ứng dụng tự đăng nhập nền bằng tài khoản sinh viên demo (`sinhvien`).
Cần có ít nhất một đề ở trạng thái **Đã xuất bản** (làm ở website giảng viên).

## Cấu trúc

| File | Vai trò |
|---|---|
| `src/api.js` | Gọi API `/api/services/app/StudentExam/*`, tự lấy token, chuẩn hóa lỗi |
| `src/saver.js` | Lưu đáp án: revision theo câu, một yêu cầu/câu, tự thử lại, bản đệm cục bộ, `flush` trước khi nộp |
| `src/screens/list.js` | M1 danh sách đề, M2 bottom sheet bắt đầu, tab Kết quả |
| `src/screens/attempt.js` | M3/M4 làm bài (đồng hồ theo mốc máy chủ, bài đọc + ảnh), M5 lưới câu, M6 sắp hết giờ/lưu lỗi, M7 xác nhận nộp, M8 hết giờ |
| `src/screens/result.js` | M9 kết quả, M10 xem lại đáp án & giải thích |

## Hành vi chính

- Đồng hồ: client chỉ đếm theo `serverNowUtc`/`expiresAtUtc` do máy chủ trả; tải lại trang không reset giờ; hết giờ tự nộp.
- Lưu đáp án tự động; trạng thái "Đã lưu / Đang lưu… / Lưu lỗi · Thử lại". Chạm lại lựa chọn đang chọn (hoặc "Bỏ chọn câu này") = hủy chọn → tính là bỏ qua.
- Thứ tự câu/lựa chọn do máy chủ trộn và lưu; mở lại bài giữ nguyên.
- API làm bài không trả đáp án; đáp án đúng và giải thích chỉ có trong màn kết quả sau khi nộp.
