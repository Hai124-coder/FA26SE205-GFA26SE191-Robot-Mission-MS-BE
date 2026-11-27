# MQTT/Webots contract

## 1. Mục đích và phạm vi

Tài liệu này là hợp đồng tích hợp duy nhất giữa backend RMMS, broker MQTT và
controller Webots. Nó cụ thể hóa phần MQTT trong
`docs/rmms-v8-system-scope.md` và `docs/rmms-v8-backend-api-updated.md`.

Giai đoạn đầu chỉ mô phỏng robot `MED-01` trên một tầng. Một chuyến được phát
từ backend, robot đi lần lượt theo các điểm dừng, rồi gửi trạng thái và sự kiện
về backend. Frontend chỉ nhận dữ liệu từ backend qua SignalR; frontend không
kết nối trực tiếp tới MQTT.

## 2. Quy tắc chung

- Mọi topic dùng mã robot (`robotCode`) như `MED-01`, không dùng khóa GUID.
- Backend phát lệnh QoS 1. Controller phải xử lý lệnh lặp an toàn.
- Callback MQTT trong controller chỉ kiểm tra và xếp hàng thông điệp. Mọi gọi
  API của Webots, gồm điều khiển motor, chạy trong simulation loop.
- Thời gian là ISO-8601 UTC. JSON dùng tên trường camelCase.
- Robot bỏ qua thông điệp sai schema, sai `robotId`, sai topic hoặc không còn
  phù hợp với trạng thái hiện tại; việc bỏ qua không được làm thay đổi chuyến.

## 3. Topics

| Topic | Chiều | QoS | Mục đích |
| --- | --- | --- | --- |
| `rmms/v1/{robotCode}/order` | Backend → robot | 1 | Phát một chuyến mới |
| `rmms/v1/{robotCode}/command` | Backend → robot | 1 | Lệnh điều khiển đơn lẻ |
| `rmms/v1/{robotCode}/state` | Robot → backend | 1 | Telemetry và sự kiện thực thi |
| `rmms/v1/{robotCode}/alert` | Robot → backend | 1 | Sự cố từ robot; chưa triển khai ở giai đoạn đầu |

## 4. `order`: phát chuyến

Backend tạo một bản ghi `RobotCommandOutbox` có `CommandType = DISPATCH_TRIP`
trong cùng transaction với `Trip`, `TripStop` và cập nhật Mission. Worker gửi
bản ghi này tới `rmms/v1/{robotCode}/order`.

```json
{
  "orderId": "TRP-EXAMPLE-001",
  "robotId": "MED-01",
  "mapVersionId": "7a7d6b41-3b68-4c76-8be8-e16c4aa07d04",
  "stops": [
    {
      "seq": 1,
      "nodeId": "F2-PHARM",
      "action": "PICKUP",
      "taskIds": ["MS-001"]
    },
    {
      "seq": 2,
      "nodeId": "F2-ICU",
      "action": "DROP",
      "taskIds": ["MS-001"]
    }
  ],
  "compartments": [
    {
      "slot": 1,
      "taskId": "MS-001",
      "item": "TH-NACL",
      "qty": 8
    }
  ]
}
```

`orderId` là `TripCode`, không phải GUID của Trip. `nodeId` là `MapNodeCode`
của điểm đến. `taskIds` và `taskId` dùng `MissionCode`, theo payload backend
hiện tại. `mapVersionId` là trường mở rộng hợp lệ để controller kiểm tra rằng
bản đồ Webots phù hợp với bản đồ đã phát hành.

Controller chỉ chấp nhận order khi:

1. `robotId` khớp mã robot của controller.
2. Có ít nhất một stop, `seq` là các số dương liên tiếp theo thứ tự tăng dần.
3. Mỗi `nodeId` tồn tại trong bảng ánh xạ node của thế giới Webots.
4. Không có order đang chạy. Nếu `orderId` trùng order đã hoàn thành hoặc đang
   chạy, controller xác nhận bằng telemetry hiện tại nhưng không khởi động lại.

Khi chấp nhận, controller lưu `activeOrderId`, bắt đầu đi đến stop đầu tiên và
gửi state `MOVING`.

## 5. `command`: điều khiển an toàn

Payload hiện hỗ trợ:

```json
{
  "commandId": "c0ec54fa-5d66-47bf-8cc4-03171d67cf5e",
  "command": "STOP",
  "issuedAt": "2026-11-25T12:00:00Z"
}
```

`command` hiện là `STOP` hoặc `RESUME`.

- `STOP`: dừng motor ngay, đặt khóa dừng an toàn và giữ thông tin order đang
  chạy để có thể xử lý tiếp sau này.
- `RESUME` ở giai đoạn đầu: bỏ khóa dừng nhưng giữ robot `IDLE`. Việc tiếp tục
  chính xác đoạn đường dở dang là một khả năng của giai đoạn sau, không được
  ngầm thực hiện.
- `commandId` phải là UUID. Controller chỉ xử lý một lần trong cửa sổ các ID
  gần nhất.

Endpoint `POST /api/v1/robots/{id}/commands` là endpoint kiểm thử/điều khiển
khẩn cấp hiện có trong code. Các endpoint nghiệp vụ `suspend`, `resume`,
`charge`, `maintenance` trong API updated cần được nối với command contract
này hoặc được đặc tả riêng trước khi dùng cho Webots.

## 6. `state`: telemetry và execution events

Robot gửi telemetry ít nhất mỗi giây:

```json
{
  "robotId": "MED-01",
  "ts": "2026-11-25T12:00:00Z",
  "x": 2.5,
  "y": 1.2,
  "theta": 0.0,
  "battery": 92,
  "speed": 0.4,
  "status": "MOVING",
  "orderId": "TRP-EXAMPLE-001",
  "nextNodeId": "F2-ICU",
  "atStopSeq": 1
}
```

`status` là một trong `IDLE`, `MOVING`, `AT_STOP`, `CHARGING`, `ERROR`.
`orderId`, `nextNodeId` và `atStopSeq` là `null` khi robot không thực hiện
chuyến.

Sự kiện thực thi sẽ dùng cùng topic `state`, thêm trường `event`:

| `event` | Bắt buộc thêm | Ý nghĩa |
| --- | --- | --- |
| `ARRIVED` | `orderId`, `atStopSeq`, `nodeId` | Robot đã đến điểm dừng và đang chờ xử lý |
| `STOP_COMPLETED` | `orderId`, `atStopSeq`, `nodeId` | Robot đã rời điểm dừng sau khi bước đó hoàn tất |
| `COMPLETED` | `orderId` | Robot đã hoàn tất điểm cuối và trở về `IDLE` |

Từ `ARRIVED` trở đi, backend phải dùng `orderId`, `atStopSeq` và `nodeId` để
xác minh thứ tự trước khi đổi `Trip`, `TripStop` hay `Mission`. Thông điệp lặp
không tạo thêm lịch sử; thông điệp sai thứ tự bị từ chối.

> Lưu ý triển khai: subscriber hiện tại mới lưu telemetry cơ bản. Trước khi
> controller phát `event`, backend phải mở rộng schema/DTO validate để nhận
> trường `event` và chuyển chúng vào Trip Execution Service.

## 7. Ánh xạ bản đồ

Mỗi `nodeId` trong order cần một tọa độ Webots xác định. Giai đoạn đầu dùng
một bảng ánh xạ versioned theo `mapVersionId` với ba node tối thiểu:

| Node | Mục đích | Tọa độ Webots (x, z) |
| --- | --- | --- |
| `F2-PHARM` | PICKUP | `(0.5, 0.0)` |
| `F2-ICU` | DROP | `(1.1, 0.0)` |
| `F2-PEDS` | DROP | `(1.1, 0.6)` |

Package `webots/maps/hospital_f2_test_map.json` là **map test**, không phải
bản đồ bệnh viện thật. Nó có cùng các node đã nêu trong API updated để test
luồng pickup/drop. `webots/config/hospital_f2_test_nodes.json` ghi rõ phép đổi
từ tọa độ backend sang mặt phẳng Webots: `webotsX = backendX - 2.0` và
`webotsZ = backendY - 2.0`.

Trước một kiểm thử end-to-end, Admin import package này vào tầng F2 rồi publish
map version mới. GUID trả về được điền vào `backendMapVersionId`; controller
chỉ nhận order có `mapVersionId` đó. Không import hay publish package này vào
database khi chưa chủ động bắt đầu kiểm thử integration.

Controller không được tự suy diễn tọa độ từ tên node. Nếu thiếu ánh xạ hoặc
`mapVersionId` không khớp, controller giữ `IDLE` và gửi alert trong giai đoạn
alert được triển khai.

## 8. Trạng thái triển khai và cổng hoàn thành

| Hạng mục | Trạng thái |
| --- | --- |
| Mosquitto cục bộ, telemetry `state`, lưu snapshot, heartbeat | Hoàn thành |
| Outbox QoS 1 cho `order` và `command` | Hoàn thành |
| Payload backend `order` theo hợp đồng này | Hoàn thành, chưa có kiểm thử contract độc lập |
| STOP/RESUME controller | Hoàn thành ở mức an toàn cơ bản |
| Controller nhận `order` | Chưa làm |
| Ánh xạ node backend ↔ Webots | Chưa làm |
| Backend xử lý ARRIVED/STOP_COMPLETED/COMPLETED | Chưa làm |
| `alert` | Chưa làm |

Chỉ bắt đầu phần controller nhận `order` sau khi có kiểm thử contract cho
payload backend và bảng ánh xạ node tối thiểu. Một chuyến được xem là hoàn
thành khi telemetry và các event làm Trip chuyển tuần tự
`DISPATCHED → IN_PROGRESS → COMPLETED`, các stop đúng thứ tự, robot trở về
`AVAILABLE`, và frontend nhận được realtime tương ứng.
