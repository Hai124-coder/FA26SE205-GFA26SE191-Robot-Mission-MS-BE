# Kết nối database Supabase

BE dùng .NET 8, EF Core 8 và Npgsql để truy cập PostgreSQL có sẵn trên Supabase.
Database hiện là nguồn cấu trúc chính (Database First).

## Cấu hình trên máy phát triển

Trong Visual Studio, nhấp phải `RobotMissionMS.Api` > **Manage User Secrets**.
Lưu chuỗi kết nối Supabase ở khóa `ConnectionStrings:DefaultConnection`.
Không lưu mật khẩu trong source code hoặc commit file secrets.
Khi triển khai, cung cấp biến môi trường `ConnectionStrings__DefaultConnection`.

Chuỗi kết nối hiện dùng Session pooler cổng 5432 và SSL. Lấy chính xác host,
username và mật khẩu từ project Supabase của bạn. Cấu hình SSL Require hiện
mã hóa đường truyền; khi triển khai nên cấu hình VerifyFull với chứng chỉ tin cậy
theo hướng dẫn Supabase, thay vì bỏ qua xác minh chứng chỉ.

## Chạy và kiểm tra

```powershell
dotnet build RobotMissionMS.sln
dotnet run --project RobotMissionMS.Api --launch-profile https
```

Mở `https://localhost:7256/swagger`, gọi `GET /health/database`.
Kết nối thành công trả HTTP 200 với `{"status":"connected"}`; không kết nối
được trả HTTP 503. Endpoint này chỉ được bật trong môi trường Development.

## Cấu trúc code

- `RobotMissionMS.DataAccess/Entities`: entity của 24 bảng nghiệp vụ trong schema public.
- `RobotMissionMS.DataAccess/Persistence/RobotMissionDbContext.cs`: cấu hình bảng và quan hệ.
- `RobotMissionMS.DataAccess/Enums/DatabaseEnums.cs`: 18 enum PostgreSQL với nhãn gốc.
- `Entities/EnumProperties.cs` và `Persistence/RobotMissionDbContext.Enums.cs`:
  bổ sung 22 cột enum cùng các index bị công cụ scaffold bỏ qua.
- `Persistence/DatabaseDataSource.cs`: đăng ký enum với Npgsql.
- `RobotMissionMS.Api/Program.cs`: đọc cấu hình, đăng ký data source và DbContext.

Business tham chiếu DataAccess. API tham chiếu Business và DataAccess để đăng ký DI.
Chưa triển khai API nghiệp vụ hoặc cơ chế xác thực/phân quyền. Kết nối trực tiếp
database không tự mang danh tính người dùng Supabase Auth; cần thiết kế phân quyền
trước khi công khai các API đọc/ghi dữ liệu.

## Khi database thay đổi

Không chạy `database update`, `EnsureCreated` hoặc tự động migrate trên database này.
Scaffold chỉ đọc metadata và sinh code, nhưng không tự ánh xạ đầy đủ enum PostgreSQL.
Khi cập nhật model, sinh vào thư mục tạm để so sánh trước, giữ các file enum bổ sung,
kiểm tra cột/enum/index mới rồi build và thử đọc lại. Các bảng hệ thống auth/storage
không thuộc model nghiệp vụ. Chưa thiết lập EF migration baseline cho database hiện có.

## Đã kiểm tra

- Build solution thành công.
- Truy vấn tối đa một dòng ở mỗi bảng bằng EF Core, không in nội dung dữ liệu.
- Kiểm tra gửi/nhận tất cả giá trị của 18 enum qua PostgreSQL.
- Không thực hiện thao tác ghi hoặc thay đổi schema.
