# Incident contract – phần 9

Contract này là nguồn luật nghiệp vụ cho Incident Service. `Alerts` hiện chỉ lưu
thông tin cốt lõi; migration 010 sẽ lưu scenario, phase, cargo, đề xuất, timeline
và nhiều nhiệm vụ liên quan.

| Scenario | Alert type | Phase đầu | Cargo | Đề xuất | Action giai đoạn DECIDE |
| --- | --- | --- | --- | --- | --- |
| A1 | NOT_RECEIVED | DECIDE | ONBOARD | GO_ON | GO_ON, WAIT_MORE |
| A4 | WRONG_SLOT | DECIDE | ONBOARD | HUMAN | HUMAN, REASSIGN, REQUEUE |
| A5 | PICKUP_SHORT | DECIDE | PICKUP | WAIT_MORE | WAIT_MORE, GO_PARTIAL |
| B1 | FAULT | DECIDE | ONBOARD | REASSIGN | HUMAN, REASSIGN, REQUEUE |
| B2 | FAULT | DECIDE | ROUTE | HUMAN | HUMAN, REASSIGN, REQUEUE |
| B4 | OBSTACLE | DECIDE | ROUTE | RESUME | RESUME, AS_FAULT |
| B5 | DISCONNECTED | DECIDE | ROUTE | WAIT_CONN | WAIT_CONN, AS_FAULT |
| B6 | LOW_BATTERY | DECIDE | ROUTE | NOTIFY | NOTIFY, AS_FAULT |
| E1 | SCHEDULE_LATE | DECIDE | NONE | NOTIFY | NOTIFY |
| E3 | AI_DOWN | DECIDE | NONE | NOTIFY | NOTIFY |
| E4 | BROKER_DOWN | DECIDE | NONE | BROKER_UP | BROKER_UP |

`HUMAN` chuyển incident sang phase `HUMAN` và yêu cầu `assignee`. Chỉ `HUMAN_DONE`
được gọi ở phase đó; action này chuyển nhiệm vụ liên quan sang `COMPLETED_BY_HUMAN`
và đóng incident. `ACCEPT` thực thi `suggestedAction`; client không truyền phase,
scenario, cargo hoặc trạng thái dữ liệu nghiệp vụ.

Các scenario A2, A3, B3, B7, C5, D5, E2 và các timeout tự động chưa có đủ quy tắc
chi tiết trong tài liệu hiện có. Chúng không nằm trong scope migration/API đầu tiên;
Incident Signal Service sẽ chỉ nhận chúng sau khi contract được xác nhận.

Payload action chuẩn:

```json
{ "action": "HUMAN", "assignee": "Phạm Văn Khoa", "note": "Giao tay tại khoa", "after": "MAINTENANCE", "targetRobotId": "00000000-0000-0000-0000-000000000000" }
```

Chỉ `assignee` cho HUMAN, `after` cho HUMAN_DONE khi cần và `targetRobotId` cho
REASSIGN được kiểm tra bắt buộc theo action; các trường khác có thể bỏ qua.
