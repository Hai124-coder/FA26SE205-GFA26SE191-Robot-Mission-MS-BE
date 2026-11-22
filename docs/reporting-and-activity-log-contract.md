# Contract tổng quan, báo cáo và nhật ký – phần 11

## 1. Quy ước chung

- API dùng tiền tố `/api/v1`.
- Tất cả timestamp trong request và response là ISO-8601 UTC. Với giá trị ngày
  không kèm giờ, `from` là 00:00:00 UTC và `to` bao gồm hết ngày đó.
- Dashboard xác định “hôm nay” theo `Asia/Ho_Chi_Minh`, nhưng vẫn trả mốc
  `generatedAt` bằng UTC.
- `floor` là `Zone.Code`. Giá trị không tồn tại trả dữ liệu rỗng, không phải lỗi.
- Khoảng thời gian phải có `from <= to`, tối đa 93 ngày. Query không hợp lệ trả
  `400` với `ProblemDetails.type` phù hợp.
- Supervisor được xem Dashboard và Report trên mọi tầng. Administrator không có
  quyền Dashboard/Report. Nhật ký cho Supervisor và Administrator.

## 2. Dashboard overview

`GET /api/v1/dashboard/overview` – Supervisor.

Response:

```json
{
  "generatedAt": "2026-11-23T03:10:00Z",
  "businessDate": "2026-11-23",
  "robotCounts": {
    "onTrip": 2,
    "charging": 1,
    "available": 4,
    "maintenance": 0,
    "error": 1,
    "inactive": 0
  },
  "missionCounts": {
    "pending": 3,
    "assigned": 1,
    "inProgress": 2,
    "deliveryFailed": 1,
    "completed": 18,
    "completedByHuman": 1,
    "rescheduled": 0,
    "cancelled": 0
  },
  "queueCount": 3,
  "openIncidentCount": 2,
  "onTime": { "eligible": 15, "onTime": 13, "rate": 0.8667 }
}
```

- Robot count lấy snapshot hiện tại từ `Robots.State`.
- Mission count lấy các mission có `CreatedAt` nằm trong ngày nghiệp vụ.
- `queueCount` là mission có status `PENDING` hoặc `RESCHEDULED` tại thời điểm
  truy vấn, không giới hạn theo ngày tạo.
- `openIncidentCount` là `Alerts` chưa `RESOLVED`.
- On-time chỉ gồm mission `COMPLETED` hoặc `COMPLETED_BY_HUMAN` có cả
  `DeadlineAt` và `CompletedAt` trong ngày nghiệp vụ. `rate` là `null` khi
  `eligible = 0`; ngược lại là `onTime / eligible`, làm tròn 4 chữ số thập phân.

## 3. Báo cáo vận hành

`GET /api/v1/reports/operations?from&to&floor&groupBy` – Supervisor.

- `from`, `to` bắt buộc; `floor` tùy chọn; `groupBy` là `hour` hoặc `day`, mặc
  định `day`.
- Mission thuộc bucket theo `CompletedAt` nếu đã hoàn tất, nếu không dùng
  `CreatedAt`. Điều này giúp báo cáo vừa có throughput hoàn tất vừa hiển thị
  mission chưa kết thúc trong khoảng chọn.
- Thời gian giao là `CompletedAt - StartedAt`, chỉ tính mission `COMPLETED` có
  đủ hai mốc và thời lượng không âm.
- Tỉ lệ đúng hạn áp dụng cùng quy tắc Dashboard, trên từng bucket.

Response:

```json
{
  "from": "2026-11-01T00:00:00Z",
  "to": "2026-11-07T23:59:59.9999999Z",
  "floor": "F1",
  "groupBy": "day",
  "buckets": [
    {
      "startAt": "2026-11-01T00:00:00Z",
      "missionTypes": [{ "code": "DELIVERY", "total": 12 }],
      "outcomes": { "completed": 9, "completedByHuman": 1, "deliveryFailed": 1, "cancelled": 1 },
      "onTime": { "eligible": 8, "onTime": 7, "rate": 0.875 },
      "averageDeliverySeconds": 812.5,
      "failureReasons": [{ "reason": "NO_RECIPIENT", "count": 1 }],
      "completedByHumanCount": 1
    }
  ]
}
```

`GET /api/v1/reports/operations/export` dùng cùng filter và yêu cầu thêm
`format=csv|xlsx`.

- CSV xuất một dòng một bucket × mission type/outcome để dễ phân tích.
- XLSX gồm `Summary` và `Breakdown`.
- Export áp dụng cùng giới hạn 93 ngày; `format` không hỗ trợ trả `400`.

## 4. Nhật ký hoạt động

`GET /api/v1/logs?actor&action&from&to&q&page&pageSize` – Supervisor, Administrator.

- `actor` là `UserId`; `action` khớp chính xác không phân biệt hoa thường.
- `q` tìm không phân biệt hoa thường trong username, full name, action và
  `targetObject`.
- `page` mặc định 1; `pageSize` mặc định 20, tối đa 100.
- Kết quả sắp xếp `timestamp DESC, id DESC`.

Response:

```json
{
  "items": [{
    "id": 125,
    "timestamp": "2026-11-23T03:00:00Z",
    "actor": { "id": "...", "username": "operator01", "fullName": "Nguyễn A" },
    "action": "TASK_CANCELLED",
    "targetObject": "Mission:...",
    "message": "TASK_CANCELLED – Mission:..."
  }],
  "page": 1,
  "pageSize": 20,
  "total": 1
}
```

`actor` là `null` cho tác nhân hệ thống; `message` được tạo bởi backend từ
`action` và `targetObject`, không thêm cột lưu trữ mới.

`GET /api/v1/logs/export` nhận cùng filter, xuất CSV, không phân trang và bị
giới hạn 50.000 dòng. Vượt giới hạn trả `409 LOG_EXPORT_LIMIT_EXCEEDED`.

## 5. Ranh giới chưa chốt

- `GET /reports/robot-performance` chưa triển khai: `Trip` không lưu quãng
  đường snapshot, còn telemetry state chưa được MQTT ghi ổn định. Cần chốt
  quãng đường kế hoạch hay thực tế trước khi quyết định migration.
- `GET /events` chưa triển khai: ActivityLog chưa bao phủ mọi operational event
  và chưa phải event store theo tầng. Endpoint này được chốt cùng phần realtime.
- Các API ghi Schedule, Charging Session và thao tác robot/trip phải được bổ
  sung ActivityLog trước khi coi nhật ký là hoàn chỉnh.
