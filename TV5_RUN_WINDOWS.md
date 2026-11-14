# HƯỚNG DẪN CHẠY ECMA – TV5 / Windows 11

## Chuẩn bị
.NET SDK 10, PostgreSQL đang chạy, PowerShell/VS Code; kiểm tra `dotnet --version`, `psql --version`. Dự án là ASP.NET Core MVC/Razor, **không phải React**.

## Tại thư mục ECMA-main
1. Mở terminal VS Code.
2. Tạo database test riêng bằng pgAdmin, ví dụ `ecma_tv5_dev`. KHÔNG trỏ vào DB production/DB của cả nhóm.
3. Copy `.env.example` thành `.env`, chỉnh `ECMA_CONNECTION_STRING` đúng username/password/Postgres/database; đặt seed Admin ở các biến `SEED_ADMIN_*` theo mẫu. KHÔNG commit `.env`.
4. Chạy:

```powershell
dotnet --version
dotnet restore ECMA.sln
dotnet tool restore
dotnet build ECMA.sln --no-restore
dotnet ef database update --project src/ECMA.Infrastructure --startup-project src/ECMA.Web
dotnet run --project src/ECMA.Web -- --seed
dotnet run --project src/ECMA.Web --launch-profile https
```

5. Mở HTTPS URL in ra ở terminal, đăng nhập bằng Admin seed. Đến **Người dùng** để thử role; đến **Quản lý sự kiện → Người tham dự → Điểm danh bằng mã** để thử check-in.
6. Điểm danh chỉ hợp lệ khi Event Published đang diễn ra và có Registration Confirmed; để test cần dựng dữ liệu với khoảng thời gian có giờ hiện tại nằm trong StartAt..EndAt và đăng ký **trước** StartAt (có thể dùng fixtures/test DB).

## Test
```powershell
dotnet test ECMA.sln --no-build --logger "console;verbosity=normal"
```
Bổ sung kiểm thử manual/integration trong `docs/testing/TV5_TEST_SPECIFICATIONS.md`. Không coi lệnh test trả code 0 với 0 tests là PASS. Nếu build lỗi, chụp output dòng lỗi đầu tiên và sửa trước khi push.

## Bàn giao GitHub
Copy mã nguồn từ ZIP mới vào clone repo của bạn, **giữ `.git` cũ**, giữ `.env` cũ và config cục bộ, không đưa `.env` vào commit. Kiểm tra `git status`, `git diff`, chạy build/test, commit/push lên nhánh riêng rồi tạo PR. Tránh ghi đè thay đổi người khác.
