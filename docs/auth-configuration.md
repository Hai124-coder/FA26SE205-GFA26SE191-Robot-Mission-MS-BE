# Cấu hình Auth

API bind hai nhóm cấu hình khi khởi động và dừng ngay nếu chúng không hợp lệ.
Không đặt JWT signing key trong `appsettings.json` hoặc source code.

## Giá trị cần có

| Khóa | Mô tả | Quy tắc |
| --- | --- | --- |
| `Jwt:Issuer` | Bên phát hành access token | Bắt buộc |
| `Jwt:Audience` | Ứng dụng nhận access token | Bắt buộc |
| `Jwt:SigningKey` | Khóa HS256, mã hóa Base64 | Bắt buộc, ít nhất 32 byte sau khi giải mã |
| `Jwt:AccessTokenMinutes` | Thời hạn access token | 1–1440 phút, mặc định 30 |
| `Auth:RefreshTokenDays` | Thời hạn tuyệt đối của phiên refresh | 1–365 ngày, mặc định 7 |

`Auth:RefreshTokenDays` không được gia hạn khi đổi refresh token trong tương lai.

## Phát triển trên máy cá nhân

Lưu các giá trị JWT trong User Secrets của `RobotMissionMS.Api`. Ví dụ, sinh một
khóa Base64 có 32 byte ngẫu nhiên rồi chạy các lệnh sau từ thư mục gốc solution:

```powershell
dotnet user-secrets set "Jwt:Issuer" "RobotMissionMS.Development" --project RobotMissionMS.Api
dotnet user-secrets set "Jwt:Audience" "RobotMissionMS.Web" --project RobotMissionMS.Api
dotnet user-secrets set "Jwt:SigningKey" "<base64-random-key>" --project RobotMissionMS.Api
dotnet user-secrets set "Jwt:AccessTokenMinutes" "30" --project RobotMissionMS.Api
dotnet user-secrets set "Auth:RefreshTokenDays" "7" --project RobotMissionMS.Api
```

User Secrets chỉ áp dụng trong môi trường `Development`. Khi chạy bằng profile
không đặt môi trường, cần đặt `DOTNET_ENVIRONMENT=Development` để đọc chúng.

## Triển khai

Đặt các biến môi trường `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey`,
`Jwt__AccessTokenMinutes` và `Auth__RefreshTokenDays`. Biến môi trường dùng hai
dấu gạch dưới thay cho dấu hai chấm. Lưu signing key trong secret store của môi
trường triển khai và luân phiên khóa theo quy trình vận hành.
