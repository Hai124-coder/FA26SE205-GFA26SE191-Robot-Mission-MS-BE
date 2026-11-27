# Biên bản kiểm thử workflow mật khẩu – phần 2

Ngày kiểm tra: 27/11/2026. Phạm vi: migration 014 và workflow xác thực/người
dùng của phần 2 trong `docs/rmms-v8-backend-api-updated.md`, trên database
Development.

## Migration 014

- Thêm `Users.MustChangePassword` và `Users.LastLoginAt`.
- Tạo `PasswordResetRequests`, enum trạng thái, unique partial index cho một
  request `OPEN` mỗi user và RLS.
- Checker xác minh hai cột, bảng, enum, index và RLS sau khi chạy script.

## Kết quả API

| Trường hợp | Kết quả |
| --- | --- |
| Login bằng mật khẩu tạm | Trả `mustChangePassword: true` |
| Token mật khẩu tạm gọi API thường | `403 PASSWORD_CHANGE_REQUIRED` |
| `/auth/me` khi bắt buộc đổi mật khẩu | Thành công |
| Đổi mật khẩu | Trả cặp token mới, `mustChangePassword: false` |
| Refresh token cũ sau đổi/reset password | `401` |
| Forgot password tồn tại/không tồn tại | Cùng `202` và cùng response |
| Admin xem/reject request | Thành công; password/session đang dùng không đổi sau reject |
| Admin reset password | Trả temporary password một lần, request thành `DONE` |
| Admin tự reset chính mình | `400 ADMIN_CANNOT_RESET_SELF` |
| Logout | `204`; refresh session đã logout trả `401` |
| Khóa account | Access token trả `401`; refresh trả `403 ACCOUNT_INACTIVE` |

## Kết thúc

User test được tạo riêng cho biên bản này đã được khóa mềm sau kiểm tra để giữ
lịch sử/audit log. Biên bản không lưu username, mật khẩu, temporary password,
access token, refresh token hoặc connection string.
