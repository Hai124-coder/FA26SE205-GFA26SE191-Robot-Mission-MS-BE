# Kiểm thử tích hợp LoginAsync

Ngày kiểm tra: 14/11/2026. Phạm vi: AuthService.LoginAsync, AuthRepository và
PostgreSQL đang cấu hình cho môi trường phát triển. Chưa bao gồm HTTP controller,
DI, JWT Bearer middleware, refresh hoặc logout.

## Cách kiểm tra

- Chạy harness console tạm, sử dụng PasswordService, JwtAccessTokenIssuer,
  RefreshTokenGenerator và AuthRepository thật. Không thay code ứng dụng.
- Tạo 8 tài khoản thử với UUID/username riêng và mật khẩu ngẫu nhiên, dùng role
  có sẵn. Không sửa tài khoản hoặc dữ liệu seed hiện có.
- Các ca thành công commit thật; dùng DbContext/kết nối riêng đọc lại kết quả.
- Với ca đồng thời, kết nối ghi giữ khóa dòng Users, request đăng nhập chạy ở
  kết nối khác. Truy vấn pg_blocking_pids xác nhận request thực sự đang chờ khóa
  trước khi kết nối ghi commit.
- Chỉ bọc repository để báo thời điểm sắp lấy khóa và chèn lỗi có chủ đích:
  log vượt giới hạn cột hoặc exception sau SaveChanges thật, trước commit.

## Kết quả

Tất cả 24 assertion đạt, gồm xác nhận dọn dữ liệu:

| Trường hợp | Kết quả |
| --- | --- |
| Đăng nhập đúng | Commit phiên và log; đọc được từ kết nối khác |
| Refresh token | Chỉ lưu hash; hash khớp token trả về |
| Phiên đăng nhập | JWT sid, RefreshTokens.FamilyId và log cùng phiên |
| Thời hạn | 7 ngày; thời gian database khớp response trong độ chính xác timestamp PostgreSQL |
| Sai mật khẩu / username không tồn tại | Cùng InvalidCredentials |
| Tài khoản không hoạt động | AccountInactive, không tạo phiên hoặc log thành công |
| Hash Identity V2 | Nâng cấp hash và commit; mật khẩu vẫn xác minh được |
| Lỗi constraint khi lưu log | Rollback hash nâng cấp, phiên và log |
| Exception sau SaveChanges, trước commit | Rollback hash nâng cấp, phiên và log |
| Khóa tài khoản trong lúc đăng nhập chờ khóa dòng | Đọc trạng thái mới sau commit, từ chối đăng nhập |
| Đổi mật khẩu trong lúc đăng nhập chờ khóa dòng | Đọc hash mới sau commit, từ chối mật khẩu cũ |

## Dọn dữ liệu và giới hạn

Khối finally xóa đúng token, log và tài khoản theo danh sách UUID của lần thử,
trong một transaction. Kết nối mới xác nhận cả ba bảng không còn dữ liệu thử.
Sequence của ActivityLogs có thể tăng dù các dòng thử đã bị xóa/rollback.
Không thay schema, secrets hoặc cấu hình ứng dụng. Harness tạm đã được dọn;
đây là biên bản kiểm tra, chưa phải bộ regression test tự động lưu trong repo.

Chưa kiểm tra lỗi mạng tại thời điểm commit (kết quả commit có thể không xác định),
hiệu năng dưới tải hoặc HTTP end-to-end. Bước kế tiếp là nối DI/cấu hình và
endpoint login theo phạm vi được thống nhất riêng.
