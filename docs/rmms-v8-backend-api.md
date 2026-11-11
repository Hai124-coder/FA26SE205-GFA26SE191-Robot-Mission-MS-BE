<!-- Source: RMMS v8 – Danh sách API backend (2026-11-10). Faithful Markdown transcription. -->
<!-- Status: proposed API specification; endpoint names and fields may change during backend detailed design. -->

# RMMS v8 – Danh sách API backend

Nov 10, 2026 · @Dinh Minh Tri

Danh sách API REST mà backend .NET cần có để chuyển prototype v8 thành frontend ReactJS chạy thật. Mọi logic đang nằm trong prototype (chia ngăn, lập kế hoạch, xử lý sự cố, mô phỏng robot) chuyển sang backend; frontend chỉ hiển thị và gọi API. Nghiệp vụ chi tiết xem RMMS v8 – Scope hệ thống và RMMS v8 – Xử lý cảnh báo và sự cố.

## 1. Quy ước chung

Tổng cộng 93 endpoint REST chia 10 nhóm (mục 2–11), 1 hub realtime SignalR, và 1 endpoint chỉ dùng khi demo. Danh sách này là đề xuất; tên và trường có thể đổi khi nhóm backend thiết kế chi tiết.

- Đường dẫn gốc: /api/v1. JSON, tên trường camelCase. Thời gian theo ISO 8601 có múi giờ (2026-11-10T09:30:00+07:00).
- Xác thực: JWT Bearer. Access token 30 phút, refresh token 7 ngày.
- Phân quyền: theo vai trò OPERATOR, SUPERVISOR, ADMIN. Cột “Vai trò” trong các bảng dưới ghi vai trò được gọi; Supervisor được gọi mọi API GET của phần vận hành nhưng không được gọi API ghi.
- Danh sách: ?page=1&pageSize=20&sort=createdAt:desc, trả về { items, page, pageSize, total }.
- Lỗi: chuẩn ProblemDetails của ASP.NET: { type, title, status, detail, errors }. Mã lỗi nghiệp vụ nằm trong type, ví dụ BR-11_SLOT_FULL, BR-14_PLAN_LOCKED, TASK_NOT_PENDING.
- Ghi đồng thời: những bản ghi nhiều người cùng sửa (kế hoạch, sự cố, nhiệm vụ) có version; gửi If-Match khi ghi, sai thì trả 409.
- Nhật ký: mọi API ghi đều tự ghi một dòng vào nhật ký hoạt động (ai, lúc nào, làm gì), frontend không phải gọi riêng.
Các enum dùng chung:

| Enum | Giá trị |
| --- | --- |
| TaskStatus | PENDING, RESCHEDULED, ASSIGNED, IN_PROGRESS, COMPLETED, COMPLETED_BY_HUMAN, DELIVERY_FAILED, CANCELLED |
| Priority | NORMAL, HIGH, URGENT |
| MissionType | MEDICINE_DELIVERY, SUPPLY_DELIVERY, SPECIMEN_TRANSPORT, WASTE_COLLECTION, CORRIDOR_CLEANING |
| RobotType | MED_CART, SUPPLY_CART, WASTE_CART, CLEAN_BOT |
| RobotState | IDLE, MOVING, STOPPED, RETURNING, CHARGING, OFFLINE, FAULT, MAINTENANCE, WAITING, SUSPENDED |
| IncidentType | NOT_RECEIVED, WRONG_SLOT, PICKUP_SHORT, FAULT, OBSTACLE, CONGESTION, WRONG_WAY, NO_ROUTE, DISCONNECTED, LOW_BATTERY, MEET, SCHEDULE_LATE, AI_DOWN, BROKER_DOWN |
| IncidentAction | ACCEPT, REASSIGN, REQUEUE, HUMAN, HUMAN_DONE, RESUME, WAIT_MORE, GO_ON, GO_PARTIAL, WAIT_CONN, AS_FAULT, BROKER_UP, MAINTENANCE, CHARGE, NOTIFY |
| TripStatus | ACTIVE, DONE, TRANSFERRED, ABORTED |
| LocationKind | PHARMACY, STORAGE, SPECIMEN, DEPT, WASTE_STATION, WASTE_POINT, CHARGER, CLEAN_STATION, CORRIDOR |

## 2. Xác thực và người dùng

Màn SC-02 Người dùng & vai trò và màn đăng nhập.

| Method | Endpoint | Vai trò | Mô tả |
| --- | --- | --- | --- |
| POST | /auth/login | Tất cả | Đăng nhập bằng username, password. Trả accessToken, refreshToken, thông tin người dùng và vai trò |
| POST | /auth/refresh | Tất cả | Đổi refresh token lấy access token mới |
| POST | /auth/logout | Tất cả | Thu hồi refresh token |
| GET | /auth/me | Tất cả | Người đang đăng nhập, vai trò, danh sách màn được xem và màn chỉ xem (để frontend dựng menu) |
| GET | /users | Admin | Danh sách người dùng; lọc role, active |
| POST | /users | Admin | Tạo người dùng: username, họ tên, vai trò, mật khẩu tạm |
| PUT | /users/{id} | Admin | Sửa họ tên, vai trò |
| PATCH | /users/{id}/status | Admin | Khoá hoặc mở khoá tài khoản (active: false) |
| POST | /users/{id}/reset-password | Admin | Đặt lại mật khẩu tạm |

Không xoá cứng người dùng, vì nhật ký và sự cố còn trỏ tới tên người xử lý; chỉ khoá.

## 3. Tầng, bản đồ, vị trí, trạm sạc

Màn SC-03 Tầng & bản đồ, SC-04 Vị trí & điểm dừng; bản đồ còn dùng cho SC-13 Bản đồ trực tiếp. Bản đồ gồm node (vị trí) và đoạn đường (edge) nối các node.

| Method | Endpoint | Vai trò | Mô tả |
| --- | --- | --- | --- |
| GET | /floors | Tất cả | Danh sách tầng (mã, tên, mô tả, trạng thái bảo trì) |
| POST | /floors | Admin | Thêm tầng |
| PUT | /floors/{floorCode} | Admin | Sửa tên, mô tả, ảnh nền |
| GET | /floors/{floorCode}/map | Tất cả | Bản đồ đang phát hành: node (toạ độ, loại), edge (độ dài, đang khoá không) |
| POST | /floors/{floorCode}/map-versions | Admin | Tải file bản đồ mới (bản nháp). Trả danh sách lỗi (D1) và node đang dùng bị xoá (D2) |
| GET | /floors/{floorCode}/map-versions | Admin | Lịch sử bản đồ của tầng |
| POST | /floors/{floorCode}/map-versions/{versionId}/publish | Admin | Phát hành bản nháp; từ chối nếu còn lỗi hoặc còn vướng node |
| PATCH | /floors/{floorCode}/edges/{edgeId} | Admin | Khoá hoặc mở một đoạn đường tạm thời (D3), không cần file mới |
| POST | /floors/{floorCode}/maintenance | Admin | Bật hoặc tắt bảo trì tầng: ngừng phát chuyến mới, chuyến đang chạy làm nốt (D4) |
| GET | /locations | Tất cả | Vị trí; lọc floor, kind. Dùng cho ô chọn khoa, điểm lấy |
| POST | /locations | Admin | Thêm vị trí: mã, tên, tầng, loại, node trên bản đồ |
| PUT | /locations/{code} | Admin | Sửa vị trí; với trạm sạc có thêm số cổng |
| PATCH | /locations/{code}/status | Admin | Ngừng dùng một vị trí; từ chối nếu đang có đơn chờ hoặc lịch dùng nó (trả danh sách chỗ đang dùng) |
| GET | /charging-stations | Tất cả | Trạm sạc: số cổng, cổng đang dùng, robot đang sạc, robot chờ cổng (SC-18) |
| PATCH | /charging-stations/{code} | Admin | Sửa số cổng sạc |

Nhóm bản đồ (map-versions, edges, maintenance) theo nhóm sự cố D, prototype v8 chưa có màn cho phần này.

## 4. Robot và loại robot

Màn SC-05 Robot (Admin), SC-14 Chi tiết robot, SC-13 Bản đồ trực tiếp. Vị trí và pin thay đổi liên tục nên frontend lấy ảnh chụp ban đầu bằng GET /robots, sau đó nhận cập nhật qua realtime (mục 12).

| Method | Endpoint | Vai trò | Mô tả |
| --- | --- | --- | --- |
| GET | /robot-types | Tất cả | 4 loại robot: số ngăn, kích thước và tải mỗi ngăn, tải trọng, tốc độ, loại trạm nhà |
| PUT | /robot-types/{type} | Admin | Sửa thông số; kiểm tra số ngăn × tải mỗi ngăn ≤ tải trọng |
| GET | /robots | Tất cả | Danh sách robot kèm trạng thái hiện tại: tầng, toạ độ, pin, state, connected, chuyến đang chạy, việc đang làm. Lọc floor, type, state |
| GET | /robots/{id} | Tất cả | Chi tiết: trạng thái, từng ngăn (nhiệm vụ, khối lượng cảm biến), chuyến hiện tại, sự cố đang mở |
| GET | /robots/{id}/history | Tất cả | Chuyến, sự cố, lần sạc trong khoảng thời gian from, to |
| POST | /robots | Admin | Thêm robot: mã, loại, tầng, trạm nhà, ghi chú |
| PUT | /robots/{id} | Admin | Sửa trạm nhà, ghi chú |
| DELETE | /robots/{id} | Admin | Cho robot ngừng hoạt động (xoá mềm, giữ lịch sử); từ chối nếu đang chạy chuyến |
| POST | /robots/{id}/suspend | Operator, Admin | Tạm ngưng: robot làm nốt chuyến đang chạy rồi không nhận chuyến mới |
| POST | /robots/{id}/resume | Operator, Admin | Cho robot hoạt động lại |
| POST | /robots/{id}/charge | Operator | Gửi robot rảnh về sạc |
| POST | /robots/{id}/maintenance | Operator | Đưa robot vào bảo trì |
| POST | /robots/{id}/back-to-service | Operator | Bảo trì xong, robot về trạng thái sẵn sàng |

Các lệnh trên được backend chuyển thành tin MQTT rmms/v1/{robotId}/command gửi xuống robot.

## 5. Danh mục hàng, loại nhiệm vụ, cấu hình

Màn SC-08 Danh mục hàng, SC-06 Loại nhiệm vụ & luật. Danh mục hàng còn dùng cho ô chọn món ở SC-10 và để sinh file mẫu Excel.

| Method | Endpoint | Vai trò | Mô tả |
| --- | --- | --- | --- |
| GET | /items | Tất cả | Danh mục hàng: mã, tên, loại việc, đơn vị, đóng gói, kg mỗi đơn vị, sức chứa tối đa mỗi ngăn (capPerSlot), giới hạn bởi gì. Lọc missionType, active |
| POST | /items | Admin | Thêm mặt hàng; kiểm tra capPerSlot × kg ≤ tải mỗi ngăn của loại robot |
| PUT | /items/{code} | Admin | Sửa mặt hàng; đơn đã tạo không bị chia lại |
| PATCH | /items/{code}/status | Admin | Ngừng hoặc bật lại một mặt hàng |
| GET | /mission-types | Tất cả | 5 loại nhiệm vụ: loại robot, theo lô hay theo lịch, loại điểm lấy, thời gian chờ khoa lấy hàng, đang bật không |
| PUT | /mission-types/{type} | Admin | Sửa thời gian chờ xác nhận, điểm lấy theo từng tầng |
| PATCH | /mission-types/{type}/status | Admin | Bật hoặc tắt một loại nhiệm vụ |
| GET | /config | Tất cả | Cấu hình luật: ngưỡng pin (20%), thời gian dừng ở điểm lấy (60 giây), thời gian mất heartbeat (5 giây), lịch chờ tối đa (15 phút), điểm ưu tiên theo mức, trọng số AI, β |
| PUT | /config | Admin | Sửa cấu hình luật |

## 6. Tạo nhiệm vụ và import Excel

Màn SC-10 Tạo nhiệm vụ. Import đi theo 2 bước: tải lên để kiểm tra (chưa tạo gì) rồi xác nhận (tạo nhiệm vụ từ các dòng hợp lệ). Việc chia ngăn ⌈SL / sức chứa⌉ làm ở backend.

| Method | Endpoint | Vai trò | Mô tả |
| --- | --- | --- | --- |
| POST | /requests/preview | Operator | Xem trước một đơn lẻ: khoa, mã hàng, số lượng → hợp lệ không, sẽ chia thành mấy ngăn, khối lượng từng ngăn |
| POST | /requests | Operator | Tạo đơn lẻ. Trả mã yêu cầu YC-xxxx và các nhiệm vụ đã tạo (PENDING) |
| GET | /requests/{requestCode} | Tất cả | Một yêu cầu và mọi nhiệm vụ tách từ nó |
| GET | /imports/template | Operator | Tải file mẫu .xlsx “RMMS-YC v3” sinh từ danh mục hiện tại (sheet YeuCau, DanhMuc, _RMMS) |
| POST | /imports | Operator | multipart/form-data, field file (.xlsx). Đọc và kiểm tra từng dòng, lưu lô ở trạng thái DRAFT. Trả bảng dòng: hợp lệ không, lý do (C1, C3), cách chia ngăn; cảnh báo nếu mẫu cũ |
| GET | /imports/{importId} | Operator, Supervisor | Chi tiết một lô import |
| POST | /imports/{importId}/commit | Operator | Tạo nhiệm vụ từ các dòng hợp lệ; lô thành COMMITTED, ghi số dòng nhận, loại, số nhiệm vụ sinh ra |
| DELETE | /imports/{importId} | Operator | Bỏ lô đang DRAFT |
| GET | /imports | Operator, Supervisor | Lịch sử import; lọc date, status |
| GET | /hospital-files | Operator | Hộp thư file bệnh viện gửi xuống (tên file, người gửi, giờ, đã import chưa) |
| POST | /hospital-files/{fileId}/import | Operator | Import thẳng một file trong hộp thư; kết quả giống POST /imports |

Ví dụ kết quả POST /imports:

```json
{
  "importId": "NH-003",
  "fileName": "nhu-cau-dot-9h.xlsx",
  "templateVersion": "RMMS-YC v3",
  "status": "DRAFT",
  "totalRows": 10, "validRows": 6, "invalidRows": 4, "tasksToCreate": 11,
  "rows": [
    {"rowNo": 5, "requestCode": "YC-0104", "department": "F2-ICU", "itemCode": "TH-NACL",
     "quantity": 20, "priority": "NORMAL", "deadline": "11:00", "valid": true,
     "split": [8, 8, 4]},
    {"rowNo": 8, "department": "F2-CARD", "itemCode": "MAY-THO", "valid": false,
     "errorCode": "C3", "error": "Không có trong danh mục hàng robot chở"}
  ]
}
```

## 7. Nhiệm vụ, hàng chờ, lịch sử

Màn SC-12 Hàng chờ, SC-19 Lịch sử nhiệm vụ. Một nhiệm vụ = một ngăn = một món cho một khoa.

| Method | Endpoint | Vai trò | Mô tả |
| --- | --- | --- | --- |
| GET | /tasks/queue | Operator, Supervisor | Nhiệm vụ chờ phát lệnh (PENDING, RESCHEDULED) xếp theo điểm ưu tiên, kèm từng thành phần điểm (mức, hạn, chờ, giao lại), dự kiến trễ. Lọc floor, type |
| GET | /tasks | Tất cả | Tìm nhiệm vụ: lọc status (nhiều giá trị), floor, type, department, robotId, requestCode, importId, from, to, q. Dùng cho các tab Hàng chờ và Lịch sử |
| GET | /tasks/{id} | Tất cả | Chi tiết: món, số lượng, kg, khoa, ngăn, robot, chuyến, số lần thất bại, sự cố liên quan, người giao tay |
| GET | /tasks/{id}/timeline | Tất cả | Dòng thời gian: tạo, phát lệnh, lên robot, tới khoa, cảm biến báo trống, thất bại (lý do), giao lại, huỷ |
| PUT | /tasks/{id} | Operator | Sửa khi còn PENDING/RESCHEDULED: số lượng (≤ sức chứa ngăn), ưu tiên, hạn giao, ghi chú. Lỗi TASK_NOT_PENDING nếu đã phát lệnh (C4) |
| POST | /tasks/{id}/cancel | Operator | Huỷ kèm lý do. Chỉ khi PENDING/RESCHEDULED (C4), hoặc DELIVERY_FAILED và hàng đã về kho (A1) |
| POST | /tasks/{id}/redeliver | Operator | Giao lại đơn DELIVERY_FAILED đã về kho → RESCHEDULED. Trả cảnh báo nếu đã thất bại 2 lần |

Trường actions trong kết quả GET /tasks/{id} liệt kê thao tác được phép (EDIT, CANCEL, REDELIVER) để frontend bật/tắt nút, không phải tự suy từ trạng thái.

## 8. Điều phối (kế hoạch) và chuyến

Màn SC-11 Điều phối, SC-17 Chuyến đang chạy, SC-22 Lịch sử điều phối. Tại mỗi thời điểm có tối đa một kế hoạch nháp (current). Thuật toán AI, thuật toán dự phòng và 4 luật ghép chuyến (BR-10, 11, 13, cùng tầng) chạy ở backend.

| Method | Endpoint | Vai trò | Mô tả |
| --- | --- | --- | --- |
| GET | /plans/current | Operator, Supervisor | Kế hoạch nháp: nguồn (AI hoặc FALLBACK), lý do lập, từng chuyến (robot, điểm lấy, thứ tự điểm giao, giờ dự kiến, quãng đường), đơn phải chờ và lý do, đơn dự kiến trễ và vì sao, so sánh với cách xếp đơn giản, số lần chỉnh tay. 204 nếu chưa có |
| POST | /plans | Operator | Lập hoặc lập lại kế hoạch từ hàng chờ (ghi đè nháp cũ). AI không phản hồi thì dùng dự phòng và tạo cảnh báo AI_DOWN |
| PATCH | /plans/current/tasks/{taskId} | Operator | Chuyển đơn sang robot khác hoặc về chờ (robotId: null). Lỗi BR-11_SLOT_FULL, BR-10_PICKUP_MISMATCH, BR-13_WRONG_TYPE. Trả kế hoạch đã tính lại |
| PATCH | /plans/current/tasks/{taskId}/priority | Operator | Đổi ưu tiên một đơn ngay trong kế hoạch; trả kế hoạch đã tính lại |
| DELETE | /plans/current/trips/{robotId} | Operator | Bỏ một chuyến; các đơn của chuyến đó về chờ |
| DELETE | /plans/current | Operator | Bỏ kế hoạch |
| POST | /plans/current/release | Operator | Phát lệnh: kiểm tra lại robot, tạo chuyến, nhiệm vụ thành ASSIGNED, gửi MQTT order. Nếu có robot không còn sẵn sàng: 409 PLAN_STALE kèm kế hoạch mới đã lập lại |
| GET | /plans | Operator, Supervisor | Lịch sử các lần điều phối: giờ, người phát, nguồn, số chuyến, số đơn, số lần chỉnh tay, trễ dự kiến so với thực tế. Lọc from, to |
| GET | /plans/{planId} | Operator, Supervisor | Chi tiết một lần điều phối đã phát |
| GET | /trips | Tất cả | Chuyến; lọc status (ACTIVE cho SC-17), kind (DELIVERY, REASSIGN, WASTE, CLEAN), robotId, floor, from, to |
| GET | /trips/{tripId} | Tất cả | Chi tiết chuyến: điểm dừng, điểm hiện tại, từng ngăn (nhiệm vụ, đã lấy chưa, lúc nào) |

Backend tự soạn kế hoạch khi một robot vừa rảnh mà còn đơn chờ đúng loại, rồi bắn sự kiện realtime plan.drafted để màn Điều phối hiện thông báo. Sau khi phát lệnh, chuyến chỉ đổi được qua API sự cố (BR-14).

## 9. Cảnh báo và sự cố

Màn SC-16 Cảnh báo & sự cố. Backend phát hiện sự cố từ tin MQTT hoặc từ bộ đếm giờ (hết giờ chờ, mất heartbeat), tính đề xuất, và tự xử lý các trường hợp không cần người (B1 có robot thay, B3 robot tự vượt, A2, A1 quá 2 phút không phản hồi). Frontend chỉ cần một API hành động cho mọi nút.

| Method | Endpoint | Vai trò | Mô tả |
| --- | --- | --- | --- |
| GET | /incidents | Operator, Supervisor | Danh sách; lọc status (OPEN, RESOLVED), category (ROBOT, MISSION, SYSTEM), type, robotId, from, to |
| GET | /incidents/{id} | Operator, Supervisor | Chi tiết: loại, mức độ, robot, chuyến, hàng đang ở đâu (cargo: ONBOARD, PICKUP, ROUTE, NONE), nhiệm vụ bị ảnh hưởng, giai đoạn (SELF, DECIDE, HUMAN, DONE), đề xuất, danh sách hành động được phép kèm nhãn, mã tình huống tài liệu (A1, B5 → B2…) và trạng thái tài liệu, dòng thời gian |
| POST | /incidents/{id}/actions | Operator | Thực hiện một hành động (bảng dưới). Sai giai đoạn hoặc không được phép: 409 ACTION_NOT_ALLOWED |
| GET | /incidents/stats | Operator, Supervisor | Đang mở, đã xử lý (do Operator / tự động), thời gian xử lý trung bình, số nhiệm vụ giao tay. Lọc from, to |

Nội dung gửi lên POST /incidents/{id}/actions:

| action | Dùng cho | Trường thêm | Kết quả |
| --- | --- | --- | --- |
| ACCEPT | Mọi sự cố có đề xuất | — | Làm đúng đề xuất của hệ thống |
| GO_ON | A1 | reason (mặc định “Người nhận vắng”) | Robot đi tiếp, nhiệm vụ còn hàng DELIVERY_FAILED |
| WAIT_MORE | A1, A5 | — | A1: chờ thêm 2 phút. A5: kho có thêm 60 giây |
| GO_PARTIAL | A5 | — | Đi với phần đã có, đơn thiếu RESCHEDULED |
| HUMAN | A4, B1, B2, chuyến định kỳ | assignee (tên người đi) | Sự cố sang giai đoạn HUMAN |
| HUMAN_DONE | Sau HUMAN | after: MAINTENANCE hoặc CHARGE (không cần cho A4) | Nhiệm vụ COMPLETED_BY_HUMAN, đóng sự cố |
| REASSIGN | B1, chuyến định kỳ | targetRobotId (tuỳ chọn) | Giao cả chuyến cho robot cùng loại |
| REQUEUE | B1 | — | Nhiệm vụ về hàng chờ ưu tiên cao (RESCHEDULED) |
| RESUME | B4 | — | Đường đã thông, robot chạy tiếp |
| WAIT_CONN | B5 | — | Chờ robot kết nối lại; kết nối lại thì tự đóng |
| AS_FAULT | B4, B5, B6 | — | Coi như robot hỏng, tính lại đề xuất theo B1/B2 |
| BROKER_UP | E4 | — | Xác nhận broker chạy lại, kết nối lại các robot |
| MAINTENANCE, CHARGE | Robot rảnh báo lỗi | — | Gửi robot đi bảo trì hoặc về sạc |
| NOTIFY | B6 15%, E1, E3 | note (tuỳ chọn) | Ghi nhận, đóng cảnh báo |

Ví dụ: POST /incidents/SC-014/actions với {"action":"HUMAN","assignee":"Phạm Văn Khoa"}, giao xong gọi tiếp {"action":"HUMAN_DONE","after":"MAINTENANCE"}.

## 10. Lịch định kỳ và sạc

Màn SC-15 Lịch định kỳ, SC-18 Trạm sạc. Tới giờ, backend tự tạo chuyến gom rác hoặc lau hành lang cho robot đúng loại; robot bận quá 15 phút thì tạo cảnh báo SCHEDULE_LATE (E1).

| Method | Endpoint | Vai trò | Mô tả |
| --- | --- | --- | --- |
| GET | /schedules | Operator, Supervisor | Danh sách lịch: loại việc, tầng, giờ, lặp lại, đang bật, lần chạy gần nhất và kết quả, lần chạy kế tiếp |
| POST | /schedules | Operator | Tạo lịch: missionType (WASTE_COLLECTION, CORRIDOR_CLEANING), floor, time, recurrence |
| PUT | /schedules/{id} | Operator | Sửa lịch |
| PATCH | /schedules/{id}/status | Operator | Bật hoặc tắt lịch |
| POST | /schedules/{id}/run | Operator | Chạy ngay một lượt, không chờ tới giờ |
| DELETE | /schedules/{id} | Operator | Xoá lịch |
| GET | /charging-sessions | Tất cả | Lịch sử sạc: robot, trạm, giờ vào, giờ ra, pin đầu và cuối, thời gian chờ cổng. Lọc robotId, station, from, to |

Trạng thái trạm sạc hiện tại nằm ở GET /charging-stations (mục 3). Robot tự về sạc khi pin dưới 20% là việc của backend, không cần API.

## 11. Tổng quan, báo cáo, nhật ký

Màn SC-20 Tổng quan, SC-21 Báo cáo vận hành, SC-23 Hiệu suất robot, SC-07 Nhật ký. Các số tổng hợp tính ở backend, frontend chỉ vẽ.

| Method | Endpoint | Vai trò | Mô tả |
| --- | --- | --- | --- |
| GET | /dashboard/overview | Supervisor | Hôm nay: số robot theo trạng thái (đang làm, sạc, rảnh, bảo trì, lỗi), nhiệm vụ theo trạng thái, tỉ lệ đúng hạn, hàng chờ, sự cố đang mở |
| GET | /reports/operations | Supervisor | Báo cáo vận hành theo from, to, floor, groupBy (hour, day): số nhiệm vụ theo loại và kết quả, tỉ lệ đúng hạn, thời gian giao trung bình, giao không thành công theo lý do, giao tay |
| GET | /reports/operations/export | Supervisor | Xuất báo cáo ra file, format = xlsx hoặc csv |
| GET | /reports/robot-performance | Supervisor | Theo từng robot: số chuyến, số đơn, quãng đường, tỉ lệ thời gian làm việc / sạc / rảnh / lỗi, số sự cố, số lần sạc |
| GET | /logs | Supervisor, Admin | Nhật ký hoạt động: thời gian, người (hoặc Hệ thống), hành động, nội dung. Lọc actor, action, from, to, q |
| GET | /logs/export | Supervisor, Admin | Xuất nhật ký ra csv |
| GET | /events | Tất cả | Sự kiện vận hành gần nhất cho khung “Diễn biến” trên bản đồ; since để lấy phần bị lỡ khi mất kết nối realtime |

## 12. Realtime cho frontend và cầu nối MQTT

Frontend không nói chuyện trực tiếp với MQTT. Backend nghe MQTT từ robot, cập nhật dữ liệu, rồi đẩy thay đổi xuống trình duyệt qua SignalR (WebSocket của .NET).

### SignalR hub /hubs/ops

Kết nối kèm access token. Sau khi kết nối, client gọi JoinFloor(floorCode) để chỉ nhận sự kiện robot của tầng đang xem; sự kiện khác gửi theo vai trò.

| Sự kiện server gửi | Khi nào | Dữ liệu | Màn dùng |
| --- | --- | --- | --- |
| robot.updated | Mỗi lần robot gửi state (khoảng 1 giây), đổi trạng thái, đổi kết nối | id, floor, x, y, battery, state, connected, tripId, điểm đang tới | SC-13, SC-14, SC-17, SC-18 |
| task.updated | Nhiệm vụ đổi trạng thái | id, status, robotId, slot, reason | SC-12, SC-17, SC-19 |
| trip.updated | Tới hoặc rời điểm dừng, ngăn được lấy, chuyến kết thúc | tripId, idx, stops, slots | SC-17, SC-13 |
| incident.created / incident.updated | Có cảnh báo mới hoặc sự cố đổi giai đoạn | bản tóm tắt sự cố | SC-16, thông báo góc màn hình |
| plan.drafted | Backend tự soạn kế hoạch (robot vừa rảnh) | planId, lý do | SC-11 |
| queue.changed | Thêm, sửa, huỷ nhiệm vụ chờ | số đơn chờ theo tầng | SC-12, menu đếm số |
| import.received | File mới vào hộp thư bệnh viện | fileId, tên file | SC-10 |
| event.logged | Sự kiện vận hành mới | thời gian, loại, nội dung | Khung diễn biến SC-13 |

### Cầu nối MQTT (nội bộ backend)

| Topic | Backend làm gì |
| --- | --- |
| Nghe rmms/v1/+/state | Cập nhật vị trí, pin, heartbeat. Xử lý sự kiện ARRIVED (bắt đầu đếm giờ chờ), COMPARTMENT_EMPTY (nhiệm vụ COMPLETED, hoặc WRONG_SLOT nếu sai khoa), BUTTON_PRESSED, STOP_COMPLETED |
| Nghe rmms/v1/+/alert | Tạo sự cố, tính đề xuất, tự xử lý nếu được |
| Gửi rmms/v1/{id}/order | Khi phát lệnh, giao lại chuyến (REASSIGN), chạy lịch, thêm điểm RETURN |
| Gửi rmms/v1/{id}/command | CLOSE_AND_CONTINUE, dừng, tiếp tục, về sạc, bảo trì |
| Theo dõi kết nối broker | Mất broker thì tạo một sự cố BROKER_DOWN (E4), không tạo hàng loạt B5 |

Bộ đếm giờ chạy nền (BackgroundService) cần có: hết giờ chờ ở khoa (A1), 15 giây tự đi (A2), 2 phút chờ Operator (A1), hết giờ xếp hàng (A5), heartbeat 5 giây (B5), giờ lịch định kỳ và 15 phút chờ (E1), điểm chờ tăng dần trong hàng chờ.

### Endpoint chỉ dùng khi demo

Khung “Giả lập tình huống” của prototype không có trong hệ thống thật: sự cố đến từ Webots. Nếu nhóm muốn giữ khung này để demo, thêm POST /dev/scenarios với { "code": "B2", "robotId": "MED-01" }, chỉ bật ở môi trường Development; backend gửi lệnh tạo tình huống sang controller Webots.

## 13. Màn hình gọi API nào, và thứ tự nên làm

| Màn | API khi mở màn | API khi bấm nút | Realtime |
| --- | --- | --- | --- |
| Đăng nhập, menu | — | POST /auth/login, GET /auth/me | — |
| SC-13 Bản đồ trực tiếp | GET /floors/{f}/map, GET /robots?floor=, GET /events | — | robot.updated, trip.updated, event.logged |
| SC-14 Chi tiết robot | GET /robots/{id}, GET /robots/{id}/history | suspend, resume, charge, maintenance, back-to-service | robot.updated |
| SC-10 Tạo nhiệm vụ | GET /items, GET /locations?kind=DEPT, GET /hospital-files, GET /imports | POST /requests/preview, POST /requests, GET /imports/template, POST /imports, POST /imports/{id}/commit, DELETE /imports/{id} | import.received |
| SC-12 Hàng chờ | GET /tasks/queue, GET /tasks?status=DELIVERY_FAILED | PUT /tasks/{id}, POST /tasks/{id}/cancel, POST /tasks/{id}/redeliver | queue.changed, task.updated |
| SC-11 Điều phối | GET /plans/current | POST /plans, PATCH .../tasks/{id}, PATCH .../priority, DELETE .../trips/{robotId}, DELETE /plans/current, POST /plans/current/release | plan.drafted, robot.updated |
| SC-17 Chuyến đang chạy | GET /trips?status=ACTIVE | — | trip.updated, task.updated |
| SC-16 Cảnh báo & sự cố | GET /incidents, GET /incidents/{id}, GET /incidents/stats | POST /incidents/{id}/actions | incident.created, incident.updated |
| SC-15 Lịch định kỳ | GET /schedules | POST, PUT, status, run, DELETE lịch | — |
| SC-19 Lịch sử nhiệm vụ | GET /tasks, GET /tasks/{id}/timeline | — | — |
| SC-18 Trạm sạc | GET /charging-stations, GET /charging-sessions | PATCH /charging-stations/{code} (Admin) | robot.updated |
| SC-20 Tổng quan | GET /dashboard/overview | — | robot.updated, incident.created |
| SC-21 Báo cáo vận hành | GET /reports/operations | GET /reports/operations/export | — |
| SC-22 Lịch sử điều phối | GET /plans, GET /plans/{id} | — | — |
| SC-23 Hiệu suất robot | GET /reports/robot-performance | — | — |
| SC-07 Nhật ký | GET /logs | GET /logs/export | — |
| SC-02 Người dùng | GET /users | POST, PUT, status, reset-password | — |
| SC-03, SC-04 Tầng, vị trí | GET /floors, GET /floors/{f}/map, GET /locations | API nhóm 3 | — |
| SC-05 Robot | GET /robot-types, GET /robots | API nhóm 4 | robot.updated |
| SC-06, SC-08 Luật, danh mục | GET /mission-types, GET /config, GET /items | API nhóm 5 | — |

### Thứ tự nên làm

Làm theo thứ tự dưới để mỗi bước có thể demo được ngay.

1. Xác thực, người dùng, menu theo vai trò (nhóm 2).
2. Dữ liệu nền và seed: tầng, vị trí, loại robot, danh mục hàng, loại nhiệm vụ, cấu hình (nhóm 3, 5; chỉ cần các API GET trước).
3. Robot + cầu nối MQTT + SignalR robot.updated: bản đồ trực tiếp thấy robot Webots chạy.
4. Tạo nhiệm vụ, import Excel, hàng chờ (nhóm 6, 7).
5. Lập kế hoạch, phát lệnh, chuyến (nhóm 8): bắt đầu bằng thuật toán dự phòng vì đơn giản, thêm AI sau.
6. Giao hàng bằng cảm biến và các bộ đếm giờ A1, A2; dòng thời gian nhiệm vụ.
7. Cảnh báo và sự cố (nhóm 9), bắt đầu từ A1, B1, B2 rồi tới các loại còn lại.
8. Lịch định kỳ và sạc (nhóm 10).
9. Tổng quan, báo cáo, nhật ký, các API ghi của Admin (nhóm 11 và phần còn lại của 3, 4, 5).
