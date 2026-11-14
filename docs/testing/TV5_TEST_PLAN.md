# TV5 – Test Plan (Bùi Duy Mạnh)

## 1. Phạm vi
Dựa trên TaskSheet Nhóm 9: (15) Participant Check-in, (16) User & Role Management, (17) Database & Architecture, (18) Integration & Security, (19) Testing & Fixes. Nguồn nghiệp vụ: `docs/product/SRS_ECMA.md` §§3.7, 3.8, 4.4, 7.

## 2. Môi trường
Windows 11, .NET SDK 10, PostgreSQL, Chrome/Edge, database test độc lập; seed hai Admin Active và tài khoản Organizer, Attendee. Dùng giờ UTC cho DB, Asia/Ho_Chi_Minh cho hiển thị. Không test bằng database vận hành.

## 3. Chiến lược
- Unit: chuẩn hóa mã, trạng thái và vai trò domain.
- Integration: truy vấn PostgreSQL, transaction, kiểm tra FK/check constraint/index, đồng thời check-in và đổi quyền.
- HTTP/UI: đăng nhập theo từng role, antiforgery trên POST, form/filter/PRG.
- Regression: chạy toàn bộ test dự án, test các flow đăng ký/hủy và tạo sự kiện đã tồn tại.

## 4. Tiêu chí
Đầu vào: PostgreSQL test truy cập được, `dotnet restore/build` hoàn thành và dữ liệu test có Event Draft/Published/Completed cùng Registration Confirmed/Cancelled.
Đạt: không lỗi build; tất cả TC mức P0/P1 thực thi PASS; đồng thời không vi phạm ràng buộc; không có test bị bỏ qua ngoài trường hợp được nhóm duyệt. Chưa có chứng cứ runtime thì trạng thái là **NOT RUN**, không phải PASS.

## 5. Lệnh kiểm tra
```powershell
dotnet --version
dotnet restore ECMA.sln
dotnet build ECMA.sln --no-restore
dotnet test ECMA.sln --no-build --logger "console;verbosity=normal"
```
Cần chạy kiểm thử DB với connection string TEST riêng (xem `docs/development/verification.md`). Ghi output, ngày, commit SHA, OS và lỗi vào Test Execution Record.

## 6. Quản lý lỗi
Severity: Blocker (mất dữ liệu/leak quyền), Critical (sai điểm danh/quyền), Major (sai chức năng), Minor (giao diện). Mỗi bug: ID, steps, expected/actual, screenshot/log loại bỏ secrets, commit sửa, retest.

## 7. Trạng thái tại thời điểm bàn giao
Các file test đã được viết nhưng **chưa thực thi** do môi trường tạo gói không có `dotnet`/PostgreSQL. Không kết luận pass hay production-ready.
