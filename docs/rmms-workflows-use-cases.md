# RMMS — Workflows & Use Cases

> **Nguồn:** `RMMS - FinalV2.docx` (6 trang).  
> **Mục đích:** Tài liệu tham chiếu nghiệp vụ cho người phát triển và AI coding assistants.  
> **Phạm vi:** 5 workflows (WF-01–WF-05), 31 use cases (UC01–UC31) và 5 non-functional requirements (NFR-01–NFR-05).  
> **Quy ước:** Nội dung dưới đây chuyển từ tài liệu gốc sang Markdown; không tự bổ sung business logic. Những điểm cần làm rõ được đặt ở cuối và **không phải yêu cầu mới**.

## Mục lục

- [1. Tổng quan workflow](#1-tổng-quan-workflow)
- [2. Chi tiết workflow](#2-chi-tiết-workflow)
  - [WF-01 — Robot Registration, Onboarding & Activation](#wf-01--robot-registration-onboarding--activation)
  - [WF-02 — Mission Creation & Assignment](#wf-02--mission-creation--assignment)
  - [WF-03 — Mission Execution & Real-Time Monitoring](#wf-03--mission-execution--real-time-monitoring)
  - [WF-04 — Exception Handling & Mission Recovery](#wf-04--exception-handling--mission-recovery)
  - [WF-05 — Mission Priority Prediction & Queue Re-ordering](#wf-05--mission-priority-prediction--queue-re-ordering)
- [3. Danh sách Use Case](#3-danh-sách-use-case)
- [4. Non-Functional Requirements](#4-non-functional-requirements)
- [5. Các quy tắc nghiệp vụ quan trọng](#5-các-quy-tắc-nghiệp-vụ-quan-trọng)
- [6. Các điểm chưa được đặc tả chi tiết](#6-các-điểm-chưa-được-đặc-tả-chi-tiết)

## 1. Tổng quan workflow

| WF | Tên | Actor chính | Actor phụ | Ghi chú |
|---|---|---|---|---|
| WF-01 | Robot Registration, Onboarding & Activation | Administrator | Robot / Simulator | Admin upload bản đồ mới, config mới (không sửa trực tiếp bản đồ), đăng ký 4 loại robot (`MED_CART`, `SUPPLY_CART`, `WASTE_CART`, `CLEAN_BOT`); robot gửi heartbeat để xác nhận online. |
| WF-02 | Mission Creation, Scheduling & Robot Assignment | Operator | AI Scheduler, Robot | Operator tạo nhiệm vụ hoặc import theo lô (CSV/XLSX cấp trên gửi); chỉ được hủy khi chưa dispatch; AI Scheduler ghép đơn (batching) và gán robot. |
| WF-03 | Mission Execution & Real-Time Monitoring | Robot | Operator, Supervisor | Robot thực hiện chuyến giao hàng (theo các đơn đã ghép) hoặc chạy theo lịch; Operator giám sát và can thiệp; Supervisor chủ yếu xem trạng thái. |
| WF-04 | Exception Handling & Mission Recovery | Operator | Robot, AI Scheduler, Supervisor | Hệ thống/robot phát hiện lỗi; Operator quyết định xử lý; scheduler có thể đề xuất robot thay thế. |
| WF-05 | Mission Prioritization & Dynamic Queue Re-ordering | AI Scheduler / System | Operator, Supervisor | AI Scheduler tính priority, sắp xếp lại hàng đợi, ghép các đơn cùng điểm lấy vào chuyến tối ưu; Operator có thể điều chỉnh trước dispatch. |

## 2. Chi tiết workflow

### WF-01 — Robot Registration, Onboarding & Activation

**Actor chính:** Administrator. **Actor phụ:** Robot / Simulator.

**Luồng chính:**

1. Admin upload **bản đồ mới** và **file config mới** cho hệ thống. Admin **không chỉnh sửa trực tiếp bản đồ**.
2. Admin tạo/đăng ký robot với các thông tin:
   - `Robot ID`
   - `Robot type`: `MED_CART`, `SUPPLY_CART`, `WASTE_CART`, `CLEAN_BOT`
   - `Capabilities` / số ngăn chứa (`Slots`)
   - `Home floor` / `Operating zone`
   - `Home charging station`
3. Gán `Robot / Simulator endpoints`.
4. Hệ thống nhận `heartbeat` từ robot.
5. Validate kết nối.
6. Đặt trạng thái robot thành `Available / Ready` (cách ghi trong tài liệu gốc).
7. Robot tham gia fleet thành công; ghi `activity log`.

### WF-02 — Mission Creation & Assignment

**Actor chính:** Operator. **Actor phụ:** AI Scheduler, Robot.

**Luồng chính:**

1. Operator tạo một nhiệm vụ hoặc import nhiệm vụ theo lô từ file `CSV` / `XLSX` do cấp trên gửi.
2. Chọn loại nhiệm vụ tương ứng với bốn loại robot: `MED_CART`, `SUPPLY_CART`, `WASTE_CART`, `CLEAN_BOT`.
3. Nhập hoặc đọc từ file import:
   - Địa điểm lấy hàng
   - Địa điểm trả hàng
   - Mức độ ưu tiên
   - Hạn chót (`deadline`)
   - Mô tả hàng
   - Mô tả nhiệm vụ
4. Hệ thống kiểm tra: địa điểm có trên bản đồ đã upload, hạn chót trong tương lai, loại nhiệm vụ hợp lệ, sức chứa ngăn hợp lệ, v.v.
5. Validate nhiệm vụ; với import theo lô, validate từng dòng.
6. Nhiệm vụ vào hàng đợi với trạng thái `PENDING`. Khi còn `PENDING` (chưa dispatch), Operator có thể **sửa hoặc hủy** nhiệm vụ.
7. Gửi các nhiệm vụ `PENDING` đến `Scheduling queue`.
8. AI Scheduler thực hiện **Order Batching**: nhóm các nhiệm vụ có **cùng điểm lấy hàng** vào một chuyến dựa trên số ngăn của robot; lọc robot phù hợp.
9. Xếp hạng robot phù hợp theo các tiêu chí:
   - Đúng một trong bốn loại robot cần cho nhiệm vụ
   - Trạng thái `AVAILABLE`
   - Capabilities / số ngăn đủ điều kiện
   - Pin đủ cho **toàn bộ chuyến đã ghép**, bao gồm dự phòng để về trạm sạc
10. Chọn robot tốt nhất cho chuyến đã ghép → `ASSIGNED` (dispatch) → publish order tới topic của robot. **Không được hủy nhiệm vụ sau khi đã dispatch.**
11. Ghi log lịch sử import, ghép đơn và tạo mission.

### WF-03 — Mission Execution & Real-Time Monitoring

**Actor chính:** Robot. **Actor phụ:** Operator, Supervisor.

**Luồng chính:**

1. Dispatch/assign nhiệm vụ hoặc chuyến gồm nhiều đơn cho robot.
2. Robot nhận/accept nhiệm vụ.
3. Xác nhận bằng trạng thái `IN_PROGRESS`.
4. Robot đi đến điểm nhận hàng và publish state **mỗi giây**, gồm vị trí, pin, tốc độ, v.v.
5. Hệ thống kiểm tra, lưu và cập nhật liên tục `latest_heartbeat` (chuẩn hóa cách viết của `lastest_heartbeat` trong file gốc).
6. Robot đến điểm nhận hàng và pickup các đơn trong chuyến ghép.
7. Robot di chuyển đến từng điểm giao theo thứ tự AI Scheduler đã sắp xếp.
8. Hệ thống liên tục nhận dữ liệu robot gửi về.
9. Cập nhật dashboard.
10. Robot đến điểm giao hàng.
11. Giao hàng thành công.
12. Trạng thái nhiệm vụ → `COMPLETED`; trạng thái robot → `AVAILABLE`.

### WF-04 — Exception Handling & Mission Recovery

**Actor chính:** Operator. **Actor phụ:** Robot, AI Scheduler, Supervisor.

**Luồng chính:**

1. Hệ thống phát hiện exception, ví dụ: vật chắn, đường đông, sai hướng đi.
2. Tạo alert kèm severity, robot liên quan và nhiệm vụ liên quan.
3. Kiểm tra robot có thể tự xử lý hay không.
4. **Nếu có:** `retry` / `wait` / `reroute` → nếu thành công thì `resume mission`.
5. **Nếu không:** dừng nhiệm vụ; trạng thái robot → `Error / Blocked`; đánh giá chiến lược mission recovery.
6. Xác định recovery strategy dựa trên:
   - Độ ưu tiên
   - Loại hàng hóa
   - Quãng đường còn lại
   - Loại exception
   - Sự sẵn có của robot phù hợp khác
   - Độ khẩn cấp (`urgency`)
7. Chọn cách xử lý. **Không cancel mission đã dispatch.**
   - **Robot hỏng trước khi lấy hàng:** Giao lại **toàn bộ chuyến** cho robot khác **cùng loại**; robot mới bắt đầu từ điểm lấy hàng. Nếu không còn robot rảnh, Operator chọn **giao tay (Human Intervention)** hoặc đưa nhiệm vụ trở lại hàng chờ với ưu tiên cao.
   - **Robot hỏng sau khi lấy hàng:** Mốc xác định đã lấy hàng là **đã dừng đủ thời gian ở điểm lấy**. Con người đến robot để lấy hàng và giao tay. **Không** cho robot khác quay về kho lấy hàng mới vì kho không được xuất hàng hai lần.
8. **Giả định/quy tắc nguồn:** Hàng trong robot **luôn nguyên vẹn khi robot gặp sự cố**. Quy tắc áp dụng cho mọi điểm lấy (kho hoặc khoa → khoa). Phải lấy hết hàng khỏi robot hỏng trước khi đưa robot đi bảo trì/sạc.
9. Operator nhận thông báo nếu cần quyết định hoặc can thiệp thủ công.
10. Thực hiện recovery.
11. Trạng thái kết quả nhiệm vụ thuộc một trong: `COMPLETED`, `COMPLETED BY HUMAN`, `RESCHEDULED`. Tài liệu gốc quy định **không có `FAILED`** do giả định hàng luôn nguyên vẹn. `CANCELLED` chỉ áp dụng trước dispatch trong WF-02.
12. Ghi log và kết quả recovery.

### WF-05 — Mission Priority Prediction & Queue Re-ordering

**Actor chính:** AI Scheduler / System. **Actor phụ:** Operator, Supervisor.

**Luồng chính:**

1. Nhiệm vụ mới được tạo hoặc import theo lô (`CSV`, `XLSX`).
2. Thu thập các attributes của nhiệm vụ.
3. Tính toán/dự đoán priority score từ mission type, mức ưu tiên và waiting-time bonus; **thời gian chờ càng dài thì score càng tăng**.
4. So sánh score với các nhiệm vụ khác trong queue.
5. Sắp xếp lại hàng đợi.
6. AI Scheduler ghép các nhiệm vụ **cùng điểm lấy hàng** thành một chuyến theo số ngăn robot và tối ưu thứ tự giao hàng.
7. Scheduler lập lịch và đề xuất dispatch chuyến có độ ưu tiên cao nhất. Operator vẫn có thể hủy nhiệm vụ **trước dispatch**.

**Công thức thành phần priority được nêu trong tài liệu:**

```text
Priority = Base Mission Priority
         + Emergency Modifier
         + Deadline Modifier
         + Waiting Time Modifier
         + Location Modifier
```

> Tài liệu gốc **chưa đưa ra trọng số, cách tính cụ thể hoặc dữ liệu huấn luyện** cho từng thành phần. Không suy diễn một mô hình ML hay công thức số cụ thể từ phần này.

## 3. Danh sách Use Case

| Mã | Use Case | Actor chính | Thuộc WF / Module |
|---|---|---|---|
| UC01 | Login | Admin, Operator, Supervisor | User |
| UC02 | Logout | Admin, Operator, Supervisor | User |
| UC03 | Manage Users | Admin | User |
| UC04 | Manage Roles & Permissions | Admin | User |
| UC05 | View Activity Log | Admin / Supervisor | User |
| UC06 | Register Robot (4 Types: `MED_CART`, `SUPPLY_CART`, `WASTE_CART`, `CLEAN_BOT`) | Admin | WF01 |
| UC07 | Configure Robot Information | Admin | WF01 |
| UC08 | Upload New Map & System Config (No Direct Map Edit) | Admin | WF01 |
| UC09 | Activate / Deactivate Robot | Admin | WF01 |
| UC10 | View Robot Fleet | Admin, Operator, Supervisor | Fleet |
| UC11 | View Robot Details & Current Status | Operator, Supervisor | Fleet / WF03 |
| UC12 | Create Mission / Import Mission Batch (CSV, XLSX) | Operator | WF02 |
| UC13 | Update Pending Mission (Before Dispatch) | Operator | WF02 |
| UC14 | Cancel Pending Mission (Before Dispatch Only) | Operator | WF02 |
| UC15 | Validate Mission | System | WF02 |
| UC16 | Calculate Mission Priority | AI Scheduler / System | WF05 |
| UC17 | Reorder Mission Queue | AI Scheduler / System | WF05 |
| UC18 | View Mission Queue | Operator, Supervisor | WF05 |
| UC19 | Batch Missions (Order Batching) & Find Eligible Robots | AI Scheduler | WF02 |
| UC20 | Recommend Best Robot & Batch Route | AI Scheduler | WF02 |
| UC21 | Dispatch Batch / Assign Robot to Mission | Operator/System | WF02 |
| UC22 | Track Mission Status | Operator, Supervisor | WF03 |
| UC23 | Monitor Robot Telemetry | Operator, Supervisor | WF03 |
| UC24 | View Real-Time Fleet Dashboard | Operator, Supervisor | WF03 |
| UC25 | Detect & Generate Alert | System | WF04 |
| UC26 | View / Acknowledge Alert | Operator | WF04 |
| UC27 | Recover Failed Mission | Operator/System | WF04 |
| UC28 | Perform Human Takeover | Operator | WF04 |
| UC29 | View Mission History | Operator, Supervisor | Reporting |
| UC30 | View Reports & Fleet Statistics | Supervisor / Admin | Reporting |
| UC31 | Manage Recurring Schedule (`WASTE_CART`, `CLEAN_BOT`) | Operator | WF02 |

## 4. Non-Functional Requirements

| Mã | Thuộc tính | Chỉ số | Ngưỡng / Cách đo |
|---|---|---|---|
| NFR-01 | Real-time | Độ trễ từ lúc robot gửi state đến khi dashboard cập nhật | **≤ 1 s**; dùng script so sánh timestamp. |
| NFR-02 | Reliability | Thời gian phát hiện robot mất kết nối | **≤ 5 s**; tắt simulator, đo thời điểm có alert. |
| NFR-03 | Performance | Thời gian AI ghép đơn cho **30** nhiệm vụ `PENDING` | **≤ 2 s**; test tự động với dữ liệu cố định. |
| NFR-04 | Security | Tỷ lệ endpoint từ chối đúng khi gọi bằng sai vai trò | **100%**; gọi mọi endpoint với từng vai trò. |
| NFR-05 | Reliability | Nhiệm vụ bị ảnh hưởng khi robot hỏng kết thúc ở `COMPLETED` / `COMPLETED BY HUMAN` / `RESCHEDULED` | **100%**; kiểm thử end-to-end bằng cách giả lập robot hỏng. |

## 5. Các quy tắc nghiệp vụ quan trọng

> Phần này là **bảng tra cứu nhanh**, tổng hợp từ WF-01–WF-05 phía trên, không phải yêu cầu bổ sung.

| ID tham chiếu | Quy tắc | Nguồn |
|---|---|---|
| BR-01 | Chỉ có bốn loại robot được nêu: `MED_CART`, `SUPPLY_CART`, `WASTE_CART`, `CLEAN_BOT`. | WF-01, WF-02 |
| BR-02 | Admin upload bản đồ và config mới; không sửa bản đồ trực tiếp. | WF-01 |
| BR-03 | Kết nối robot được xác thực bằng heartbeat trước khi sẵn sàng tham gia fleet. | WF-01 |
| BR-04 | Operator chỉ sửa/hủy nhiệm vụ đang `PENDING`, trước dispatch. | WF-02, WF-05 |
| BR-05 | Sau dispatch, nhiệm vụ không thể bị cancel. | WF-02, WF-04 |
| BR-06 | Batching gom nhiệm vụ có **cùng điểm lấy**, theo số slots của robot. | WF-02, WF-05 |
| BR-07 | Robot được phân công phải đúng loại, `AVAILABLE`, đủ slots/capabilities và đủ pin đi toàn chuyến cộng dự phòng về sạc. | WF-02 |
| BR-08 | Robot publish telemetry mỗi giây trong khi thực hiện nhiệm vụ. | WF-03 |
| BR-09 | Khi exception có thể tự xử lý, robot thử retry/wait/reroute rồi tiếp tục nếu thành công. | WF-04 |
| BR-10 | Hỏng trước pickup: có thể giao lại cả chuyến cho robot cùng loại; nếu không có robot rảnh, Operator chọn giao tay hoặc đưa trở lại queue ưu tiên cao. | WF-04 |
| BR-11 | Hỏng sau pickup: người đến lấy hàng từ robot hỏng và giao tay; không cho robot khác xuất lại cùng hàng từ kho. | WF-04 |
| BR-12 | Hàng luôn được giả định nguyên vẹn sau sự cố; lấy hàng ra hết trước khi đưa robot đi bảo trì/sạc. | WF-04 |
| BR-13 | Recovery result là `COMPLETED`, `COMPLETED BY HUMAN` hoặc `RESCHEDULED`; không có kết quả `FAILED` trong mô hình nêu ở nguồn. | WF-04 |
| BR-14 | Score ưu tiên tăng theo thời gian chờ và gồm năm thành phần nêu trong công thức WF-05. | WF-05 |
| BR-15 | Operator quản lý recurring schedule cho `WASTE_CART` và `CLEAN_BOT`. | UC31 |

## 6. Các điểm chưa được đặc tả chi tiết

> Đây là **những khoảng trống hoặc khác biệt thuật ngữ trong tài liệu gốc**, để AI không tự suy diễn thành quyết định thiết kế đã được chốt.

- **Priority scoring:** Chưa có giá trị số, trọng số, ngưỡng urgency, thuật toán ranking hoặc mô hình ML cụ thể cho `Base Mission Priority`, `Emergency Modifier`, `Deadline Modifier`, `Waiting Time Modifier`, `Location Modifier`.
- **Batching / route optimization:** Chưa có công thức tối ưu, giới hạn tổng quãng đường, quy tắc tách chuyến hay xử lý trường hợp nhiều điểm lấy khác nhau.
- **Dispatch authority:** UC21 ghi `Operator/System`; WF-02 nói scheduler chọn robot và dispatch, trong khi WF-05 mô tả scheduler **đề xuất** dispatch và Operator có thể thao tác trước dispatch. Cần thống nhất dispatch tự động hay yêu cầu Operator xác nhận.
- **Robot readiness:** WF-01 ghi `Available / Ready`; WF-02 và WF-03 dùng `AVAILABLE`. Chưa có bảng trạng thái robot chính thức.
- **Mission statuses:** Có `PENDING`, `ASSIGNED`, `IN_PROGRESS`, `COMPLETED`, `COMPLETED BY HUMAN`, `RESCHEDULED`, `CANCELLED`; chưa có state machine đầy đủ hoặc quy định chuyển trạng thái khi `RESCHEDULED`.
- **Pickup confirmation:** WF-04 dùng tiêu chí robot đã dừng đủ thời gian tại điểm lấy để xác định đã pickup, nhưng chưa nêu thời gian cụ thể hoặc cách xác minh hàng thực tế.
- **Human takeover:** Chưa có quy trình chi tiết về ai thực hiện giao tay, cách xác nhận đã nhận hàng, bằng chứng hoàn tất giao hàng và trách nhiệm của Operator.
- **Exception handling:** Ví dụ exception và một số hướng recovery đã có, nhưng chưa có danh sách exception type/severity hoàn chỉnh, giới hạn retry/wait/reroute hoặc timeout cụ thể.
- **Recurring missions:** UC31 chỉ xác nhận Operator quản lý lịch lặp cho `WASTE_CART` và `CLEAN_BOT`; chưa có chi tiết quy tắc lặp lịch.
- **Terminology:** UC27 có tên `Recover Failed Mission` dù WF-04 nói không có trạng thái kết quả `FAILED`. Đây là tên use case trong nguồn, không nên hiểu là trạng thái `FAILED` được hỗ trợ.

---

**Lưu ý cho AI coding assistants:** Ưu tiên nội dung workflow và use case trong tài liệu này khi triển khai các chức năng tương ứng. Không tự chuyển các phần *chưa đặc tả* thành business rules bắt buộc. Nếu cần ra quyết định thiết kế, nêu rõ đó là đề xuất chứ không phải nội dung đã được xác nhận trong nguồn.
