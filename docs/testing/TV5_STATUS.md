# Báo cáo hiện trạng TV5

## Source baseline
`ECMA-main.zip` là một snapshot GitHub ngày 25/10/2026 theo metadata archive; nguồn so sánh TaskSheet Nhóm 9 gồm nhiệm vụ 15–19 của Bùi Duy Mạnh. Không có thư mục `.git` trong ZIP.

## Đã có sẵn
- PostgreSQL EF Core DbContext/migrations cho users/events/agenda/registrations.
- Cookie auth, security stamp, session validation, đăng ký/hủy và thống kê số lượng người tham dự.
- Registration đã có `CheckedInAt` và `CheckedInBy` nhưng chưa có màn hình/luồng cập nhật check-in.

## Mới bổ sung trong gói
- `CheckInService` và trang `/events/manage/{eventId}/check-in`, server-side kiểm tra quyền/thời gian/mã; cập nhật DB trong transaction.
- `UserManagementService` và trang Admin `/admin/users`, filter/search, cập nhật quyền/trạng thái, bảo vệ Admin Active cuối cùng.
- Bổ sung điều hướng, unit test TV5, test plan và test specifications.

## Còn cần kiểm thử / giới hạn
- Chưa có công cụ .NET SDK/PostgreSQL tại môi trường đóng gói => không có kết quả build/runtime.
- Race nhiều request/DB lock và các tương tác role–event cần kiểm nghiệm bằng PostgreSQL thật.
- Chưa có test tự động integration/HTTP đầy đủ; bảng `TV5_TEST_SPECIFICATIONS.md` là thủ tục manual chưa chạy.
- Chưa kiểm tra SRS toàn dự án đến mức khẳng định hoàn thành toàn bộ 19 nhiệm vụ: phần TV5 đã được triển khai theo các FR chính, không khẳng định 100%.
- Lưu ý: không chứa `.env`, credentials hay `.git`; áp migrations trên DB test trước, backup DB nhóm trước khi dùng.
