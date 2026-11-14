# TV5 – Test Specifications & Test Cases

Các bước mặc định: chạy `dotnet ef database update` trên DB **test**, seed hai Admin Active, Organizer O1, Attendee A1, tạo Event có khung giờ phù hợp; đăng nhập trên trình duyệt HTTPS với cookie hợp lệ. Mọi POST kiểm tra CSRF. Mã kiểm thử dạng `REG-` + 12 ký tự ASCII in hoa/số. Kết quả/actual: điền khi chạy.

| ID | Ưu tiên | Test case, input/precondition | Expected result | Actual | Status |
|---|---|---|---|---|---|
| ATT-01 | P0 | Organizer chủ Event Published, trong khung giờ, nhập mã Confirmed hợp lệ | Thành công; ghi checked_in_at/by, tên tham dự | — | NOT RUN |
| ATT-02 | P0 | Admin bất kỳ điểm danh đúng Event | Thành công | — | NOT RUN |
| ATT-03 | P0 | Attendee truy cập GET/POST điểm danh | HTTP 403; không thay đổi DB | — | NOT RUN |
| ATT-04 | P0 | Organizer khác Event cố điểm danh | HTTP 403; không thay đổi DB | — | NOT RUN |
| ATT-05 | P1 | Event Draft/Cancelled | 409, không cho điểm danh | — | NOT RUN |
| ATT-06 | P0 | Hiện tại trước start_at | 409, Chưa đến giờ điểm danh | — | NOT RUN |
| ATT-07 | P0 | Hiện tại đúng start_at | Được phép | — | NOT RUN |
| ATT-08 | P0 | Hiện tại đúng end_at hoặc sau | 409, Đã hết giờ điểm danh | — | NOT RUN |
| ATT-09 | P1 | Input `  reg-abcdef123456  ` | Được chuẩn hóa REG-ABCDEF123456 | — | NOT RUN |
| ATT-10 | P1 | Input mã sai định dạng hoặc ký tự Unicode không ASCII | 400, Mã đăng ký không hợp lệ | — | NOT RUN |
| ATT-11 | P1 | Mã không tồn tại | 404, Không tìm thấy mã đăng ký | — | NOT RUN |
| ATT-12 | P1 | Mã thuộc Event khác | 409, Mã đăng ký không thuộc sự kiện này | — | NOT RUN |
| ATT-13 | P0 | Mã Registration đã Cancelled | 409, Đăng ký đã bị hủy | — | NOT RUN |
| ATT-14 | P0 | Check-in cùng mã hai lần | Lần 2 báo thời điểm điểm danh đầu tiên, dữ liệu không đổi | — | NOT RUN |
| ATT-15 | P0 | 10 request đồng thời cùng mã | Chính xác 1 ghi thành công, số lượng tăng 1 | — | NOT RUN |
| ATT-16 | P0 | POST không có antiforgery token | HTTP 400, không ghi DB | — | NOT RUN |
| USER-01 | P0 | Guest/Attendee/Organizer GET/POST `/admin/users` | Từ chối theo auth/role | — | NOT RUN |
| USER-02 | P1 | Admin xem danh sách, tìm tên/username, lọc vai trò/trạng thái | Chỉ cột cho phép, sắp ngày giảm và ID tăng | — | NOT RUN |
| USER-03 | P1 | Keyword quá 100 ký tự, page âm, filter không hợp lệ | HTTP 400 | — | NOT RUN |
| USER-04 | P0 | Admin đổi Attendee thành Organizer | Role cập nhật, cookie/phiên cũ bị vô hiệu hóa | — | NOT RUN |
| USER-05 | P0 | Admin chuyển Active thành Inactive | Không truy cập được chức năng có xác thực | — | NOT RUN |
| USER-06 | P0 | Chuyển Admin Active cuối cùng sang Inactive/Attendee | 409, không thay đổi DB | — | NOT RUN |
| USER-07 | P0 | 2 Admin Active đồng thời vô hiệu hóa nhau | Còn ít nhất 1 Admin Active | — | NOT RUN |
| USER-08 | P0 | Hạ quyền/vô hiệu hóa chủ Event còn Draft | 409, giữ nguyên | — | NOT RUN |
| USER-09 | P0 | Hạ quyền/vô hiệu hóa chủ Event Published chưa kết thúc | 409, giữ nguyên | — | NOT RUN |
| USER-10 | P1 | Chủ Event đã Completed và không còn Draft | Được phép | — | NOT RUN |
| USER-11 | P0 | Đổi role và tạo Event cùng lúc | Kết quả tuần tự hợp lệ, không Event do Attendee mới tạo | — | NOT RUN |
| USER-12 | P0 | POST cập nhật quyền thiếu antiforgery token | HTTP 400, không ghi DB | — | NOT RUN |
| DB-01 | P0 | Apply migrations lên DB trống | Bảng users/events/registrations, FK/index/constraint có đủ | — | NOT RUN |
| DB-02 | P0 | Insert hai Registration trùng code hoặc event/user | Unique constraint từ chối | — | NOT RUN |
| DB-03 | P0 | Check-in/cancel cùng Registration đồng thời | Không thể vừa Cancelled vừa CheckedIn | — | NOT RUN |
| SEC-01 | P0 | Thay GUID/ID, request thiếu login, cookie stale, SQL/HTML payload | Không vượt quyền/CSRF/XSS/SQL injection | — | NOT RUN |
| REG-01 | P1 | Chạy toàn bộ test hiện hữu | Không regression | — | NOT RUN |

### Quy trình ghi kết quả
Với mỗi TC: lưu (date, tester, build/commit, actual, PASS/FAIL/BLOCKED, evidence, defect ID). `NOT RUN` không được đổi thành `PASS` khi chưa chạy thực tế.
