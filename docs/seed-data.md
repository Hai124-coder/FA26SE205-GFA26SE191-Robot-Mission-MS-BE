# Seed dữ liệu phát triển

Seeder là chương trình nhập một bộ dữ liệu mẫu có sẵn vào database. Không phải API,
không phải migration và không chạy tự động khi mở backend.

## Bộ dữ liệu nền v1

Nguồn: `rmms-v8-system-scope.md`, được ánh xạ theo model database hiện có.
Thông số prototype là giả định để phát triển, chưa phải cấu hình production đã duyệt.

| Bảng | Số bản ghi trên database trống |
| --- | ---: |
| Roles | 3 |
| RobotTypes | 4 |
| MissionTypes | 5 |
| Items | 16 |
| Zones (tầng F2, F3) | 2 |
| MapVersions | 2 |
| MapNodes | 50 |
| MapEdges | 48 |
| Locations | 50 |
| ChargingStations | 8 |
| SystemSettings | 20 |
| **Tổng** | **208** |

Vai trò dùng tên `ADMIN`, `OPERATOR`, `SUPERVISOR`. Chưa tạo người dùng/mật khẩu.
Mỗi tầng có 25 vị trí, 4 trạm sạc (2/1/1/1 cổng). F3 không có điểm lấy mẫu.
StationType dùng `STANDARD` cho trạm thông thường, `CLEAN` cho trạm robot lau;
đây là quy ước seed cần được các service tương lai sử dụng thống nhất.

Hai bản đồ được đánh dấu là dữ liệu seed trong SourceJson, kích thước 60 × 10 m,
25 node trên một đường thẳng, nối bằng 24 cạnh hai chiều dài 2 m mỗi cạnh.
Bản đồ được đặt PUBLISHED để vị trí tham chiếu được tới node. Đây chỉ là bản đồ
cho kiểm tra dữ liệu/API, **không phải bản đồ bệnh viện hoặc Webots**, không dùng để
phát lệnh robot thực tế. SourceJson là mô tả mẫu, chưa phải hợp đồng import bản đồ.

Schema chưa có cột riêng cho khối lượng mặt hàng, tải trọng/kích thước/tốc độ robot.
Seeder không thêm các cột đó và không giả vờ đã hỗ trợ kiểm tra tải trọng.
Các khóa cấu hình trong ReferenceCatalog.Settings là đề xuất cho service sau này;
chỉ có dữ liệu trong database chưa làm thay đổi hành vi của backend.
BasePriority của loại nhiệm vụ dùng giá trị mẫu 10, khác với trọng số ưu tiên AI.

## Cách chạy trong PowerShell

Chạy từ thư mục chứa `RobotMissionMS.sln`. Công cụ dùng chung User Secrets với API:
`ConnectionStrings:DefaultConnection`. Có thể thay bằng biến môi trường
`ConnectionStrings__DefaultConnection`; biến môi trường được ưu tiên hơn secrets.
Không nhập mật khẩu vào source code. Chọn database phát triển trước khi chạy;
biến Development là chốt kiểm tra thao tác, không chứng minh database đích là dev.

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet run --project RobotMissionMS.Seeder -- --preview
dotnet run --project RobotMissionMS.Seeder -- --apply
```

- `--preview`: thử INSERT thật trong một transaction, kiểm tra ràng buộc rồi rollback.
  Không lưu dữ liệu seed; không phải chế độ chỉ đọc và vẫn có thể kích hoạt trigger
  của database. Không dùng để thử trên production.
- `--apply`: lưu toàn bộ dữ liệu nếu mọi bước thành công.
- `added`: số bản ghi mới; `skipped`: số bản ghi đã có và được giữ nguyên.
- Chạy lại trên bộ dữ liệu vừa seed sẽ báo `added=0, skipped=208`.
- Thiếu tham số hoặc môi trường không phải Development: thoát mà không kết nối DB.

Nếu API đang chạy làm khóa DLL khi build, dừng debug trong Visual Studio rồi chạy
lệnh trên. Hoặc build toàn solution với output riêng:

```powershell
dotnet build RobotMissionMS.sln -p:BaseOutputPath=bin/seed-validation/
```

## Tính nhất quán và chạy lại

- Tìm theo mã/tên/khóa nghiệp vụ, dùng lại ID đang có; không cập nhật/xóa bản ghi cũ.
- Một transaction bao trùm toàn bộ seed, gồm nhiều lần SaveChanges theo thứ tự khóa ngoại.
- PostgreSQL advisory lock ngăn hai lần chạy công cụ seed này chồng lên nhau.
  Lock không ngăn API hoặc công cụ khác ghi dữ liệu; unique constraint vẫn bảo vệ DB.
- Quan hệ không tương thích hoặc bản đồ không thuộc bộ seed sẽ khiến công cụ dừng
  và rollback, thay vì tự sửa dữ liệu người dùng.
- Không chạy EnsureCreated, Migrate hay database update.
- Cấu hình EF Location.Kind có sentinel riêng để Pharmacy (enum 0) được ghi đúng,
  không bị mặc định DEPT của database thay thế. Thay đổi này không sửa schema.

## Bổ sung demo khi làm từng module

Hiện chỉ có reference seed. Chưa thêm robot thực thể, nhiệm vụ, chuyến, sự cố,
lịch chạy, tài khoản hoặc lịch sử vận hành. Vì vậy GET các danh mục có dữ liệu sau
khi API được triển khai, còn GET nhiệm vụ/robot vẫn có thể trả danh sách trống.

Khi triển khai module, thêm seeder riêng (ví dụ RobotDemoSeeder, MissionDemoSeeder)
và chế độ chạy demo tường minh; không trộn dữ liệu vận hành vào reference seed.
Mỗi bộ demo cần mã nhận diện ổn định, dùng lại dữ liệu nền, kiểm tra khóa ngoại,
trạng thái nhất quán và chạy lại không trùng. Tài khoản demo sẽ dùng cơ chế hash
mật khẩu của module xác thực khi module đó được triển khai.

Sau khi có API, có thể thử GET tầng, vị trí, loại robot, loại nhiệm vụ và danh mục
hàng bằng Swagger. Việc seed chỉ cung cấp dữ liệu, không tự tạo các endpoint.

## Kết quả kiểm tra ngày 11/11/2026

- Build toàn solution với output riêng: thành công, 0 warning, 0 error.
- Preview trên Supabase đang cấu hình: 208 bản ghi hợp lệ, rollback thành công.
- Apply lần đầu: thêm 208 bản ghi, commit thành công.
- Apply lần hai: thêm 0, bỏ qua 208; dữ liệu đã lưu được đọc lại và không tạo trùng.
- Không thay đổi schema hoặc tạo dữ liệu vận hành.
