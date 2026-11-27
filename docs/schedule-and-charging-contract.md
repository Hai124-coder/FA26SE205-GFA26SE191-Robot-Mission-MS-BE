# Schedule and charging contract – phần 10

Tài liệu này chốt hợp đồng API và luật nghiệp vụ cho lịch định kỳ (SC-15)
và lịch sử sạc (SC-18). Đây là đầu vào cho migration và implementation phần
10. Mọi thời điểm API trả về là UTC ISO-8601; `runTime` được người dùng nhập
theo múi giờ `Asia/Ho_Chi_Minh`.

## 1. Schedule

Một schedule chỉ đại diện cho một công việc định kỳ trên một floor. Lịch chỉ
được phép dùng hai mission type có `mode = SCHEDULE`:

| `missionTypeCode` | Robot type phù hợp |
| --- | --- |
| `WASTE_COLLECTION` | `WASTE_CART` |
| `CORRIDOR_CLEANING` | `CLEAN_BOT` |

`daysOfWeek` dùng ISO weekday: `1` là thứ Hai và `7` là Chủ nhật. `runTime`
có định dạng `HH:mm`, không mang timezone. Scheduler tính lần chạy kế tiếp từ
`daysOfWeek` + `runTime` theo `Asia/Ho_Chi_Minh`, sau đó lưu/so sánh thời điểm
thực thi bằng UTC.

Payload tạo/sửa chuẩn:

```json
{
  "name": "Thu gom rác hành lang F2 sáng",
  "missionTypeCode": "WASTE_COLLECTION",
  "floor": "F2",
  "daysOfWeek": [1, 2, 3, 4, 5],
  "runTime": "08:00",
  "stops": ["F2-WASTE-01", "F2-WASTE-02", "F2-WASTE-STATION"],
  "isActive": true
}
```

Luật validate:

- `name` dài 1–100 ký tự; `daysOfWeek` không rỗng, không trùng và chỉ chứa
  số 1–7.
- `stops` có ít nhất một phần tử, thứ tự của mảng là thứ tự robot đi qua. Mỗi
  location phải đang active, thuộc `floor`, và phù hợp loại nhiệm vụ.
- Floor phải active; mission type phải active và đúng một trong hai mã nêu
  trên. `isActive` mặc định là `true` khi tạo.
- `PUT` thay toàn bộ cấu hình và điểm dừng của schedule, nhưng không thay đổi
  các schedule run đã được tạo.

## 2. API schedule

Mọi endpoint bên dưới có prefix `/api/v1`. Operator chỉ thấy và thao tác
schedule thuộc floor trong claim của họ; Supervisor thấy toàn bộ floor.

| Method | Endpoint | Vai trò | Kết quả |
| --- | --- | --- | --- |
| GET | `/schedules` | Operator, Supervisor | Danh sách schedule theo scope floor |
| POST | `/schedules` | Operator | Tạo schedule |
| PUT | `/schedules/{id}` | Operator | Sửa schedule và điểm dừng |
| PATCH | `/schedules/{id}/status` | Operator | Body `{ "isActive": true }` bật/tắt schedule |
| POST | `/schedules/{id}/run` | Operator | Yêu cầu chạy ngay một lượt thủ công |
| DELETE | `/schedules/{id}` | Operator | Xoá logic schedule khi không có lượt đang chờ |

Mỗi phần tử GET trả về `id`, `name`, `missionTypeCode`, `floor`,
`daysOfWeek`, `runTime`, `stops`, `isActive`, `lastRunAt`, `lastResult` và
`nextRunAt`. `nextRunAt` là giá trị tính động, bằng `null` khi lịch tắt.

Xoá lịch bị từ chối với `409 SCHEDULE_HAS_OPEN_RUN` nếu có run ở trạng thái
`WAITING_FOR_ROBOT` hoặc `LATE`; Operator phải tắt lịch hoặc huỷ lượt đó trước.
Tắt lịch chỉ ngăn lần lặp tương lai, không huỷ run đã tạo. Xoá là xoá logic:
schedule biến mất khỏi danh sách và scheduler nhưng lịch sử run/mission được
giữ lại. Run `MISSION_CREATED` đã có mission độc lập nên không chặn việc xoá lịch.

## 3. Schedule run

`MissionScheduleRun` là lịch sử độc lập của mỗi lần chạy; không dùng `Mission`
thay thế vì có trường hợp không tạo được mission nhưng vẫn phải ghi nhận lượt
trễ/thất bại. Mỗi run giữ immutable snapshot gồm mission type, floor và danh
sách location stop theo thứ tự lúc run được tạo. Vì vậy, sửa schedule sau đó
không đổi route của run hoặc mission đã sinh. Các trạng thái:

| Status | Ý nghĩa |
| --- | --- |
| `WAITING_FOR_ROBOT` | Đến giờ nhưng chưa có robot đủ điều kiện |
| `MISSION_CREATED` | Đã tạo mission schedule; dispatcher sẽ lập trip/phát lệnh theo luồng hiện có |
| `LATE` | Đợi robot quá `Schedule.MaxWaitMinutes`, E1 đã được tạo |
| `FAILED` | Không thể tạo run/mission do dữ liệu không còn hợp lệ |
| `CANCELLED` | Run thủ công bị huỷ trước khi tạo mission |

Một automatic run định danh bởi cặp `(ScheduleId, ScheduledForUtc)` và phải
có unique constraint. Worker được phép chạy lặp hoặc khởi động lại nhưng chỉ
tạo một run cho mỗi slot lịch. Run thủ công có `IsManual = true`, có
`RequestedByUserId`, và dùng header `Idempotency-Key` do client gửi; cùng
schedule và cùng key luôn trả về run đã tạo thay vì tạo thêm một run.

Worker xử lý một slot đến hạn như sau:

1. Tạo hoặc lấy run theo cặp định danh.
2. Tìm robot đúng type, cùng floor, `AVAILABLE`, connected và có battery đạt
   `Battery.MinDispatchPercent`.
3. Nếu có robot, tạo đúng một `Mission` liên kết run, dùng snapshot stop đã
   chốt; run chuyển `MISSION_CREATED`.
4. Nếu chưa có robot, giữ `WAITING_FOR_ROBOT` và thử lại ở chu kỳ sau.
5. Khi vượt `Schedule.MaxWaitMinutes`, chuyển `LATE` và tạo đúng một incident
   `E1 / SCHEDULE_LATE` gắn với run. E1 không được tạo lại ở các lần quét sau.

`POST /schedules/{id}/run` yêu cầu header `Idempotency-Key`, tạo run thủ công
ngay và không làm thay đổi `nextRunAt` của lịch lặp. Khi schedule đang có run
`WAITING_FOR_ROBOT` hoặc `LATE`, request này trả `409 SCHEDULE_RUN_IN_PROGRESS`.

## 4. Charging session

Một charging session biểu diễn cả thời gian xếp hàng và sạc của một robot.
Một robot chỉ có tối đa một session active (`QUEUED` hoặc `CHARGING`). Capacity
của station chỉ tính session `CHARGING`.

| Trường | Ý nghĩa |
| --- | --- |
| `queuedAt` | Robot bắt đầu yêu cầu một cổng sạc |
| `startedAt` | Robot thực sự chiếm cổng; `null` khi còn xếp hàng |
| `endedAt` | Robot rời cổng hoặc session bị ngắt |
| `batteryAtStart` | Pin tại `startedAt` |
| `batteryAtEnd` | Pin tại `endedAt` |
| `status` | `QUEUED`, `CHARGING`, `COMPLETED`, `INTERRUPTED` |

`waitingDurationSeconds` được tính `startedAt - queuedAt`, không lưu cột số
phút dư thừa. Với session chưa vào cổng, giá trị này là thời gian đã chờ đến
hiện tại; với session tạo theo dữ liệu cũ, migration backfill `queuedAt` bằng
`startedAt` để thời gian chờ là 0.

`GET /api/v1/charging-stations` trả ảnh chụp station gồm `capacity`,
`usedPorts`, `freePorts`, `chargingRobots` và `queuedRobots`. `usedPorts` chỉ
đếm session `CHARGING`; `queuedRobots` không chiếm cổng và được sắp theo
`queuedAt` tăng dần. Mỗi phần tử `chargingRobots` có `id`, `robotCode`,
`battery`, `startedAt`; mỗi phần tử `queuedRobots` có `id`, `robotCode`,
`battery`, `queuedAt`. Contract response đầy đủ nằm ở phần 3 của
`rmms-v8-backend-api-updated.md`.

`GET /api/v1/charging-sessions` cho mọi người dùng đã xác thực, nhưng Operator
chỉ xem dữ liệu floor của họ; Supervisor và Admin xem toàn bộ. Filter hỗ trợ
`robotId`, `station`, `from`, `to`, `page`, `pageSize`; kết quả mặc định mới
nhất trước và gồm robot, station, các mốc thời gian, pin đầu/cuối, status và
`waitingDurationSeconds`.

## 5. Ranh giới tích hợp hiện tại

Backend schedule có thể tạo mission và charging service có thể quản lý session
bằng fixture/service test. Xác nhận robot đã vào/rời cổng, pin đầu/cuối thực tế
và lệnh robot về sạc sẽ được nhận từ MQTT state khi broker/Webots sẵn sàng.
Không được giả vờ đã nhận các sự kiện này khi MQTT đang tắt.
