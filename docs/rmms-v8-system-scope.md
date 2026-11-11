<!-- Source: RMMS v8 - Scope hệ thống.docx | 2026-11-10 | @Dinh Minh Tri -->
<!-- Faithful Markdown transcription for project documentation and AI reference. Prototype assumptions are NOT approved requirements. -->

# RMMS v8 – Scope hệ thống

Nov 10, 2026 · @Dinh Minh Tri

> **Trạng thái tài liệu:** Mô tả phạm vi **prototype v8**, không đồng nghĩa mọi tham số đã được nhóm phê duyệt. Các giả định chưa chốt nằm ở Mục 12.
> **Đối chiếu nguồn:** Khi khác biệt với tài liệu workflow cũ, cần xác nhận phiên bản chuẩn trước khi triển khai.

RMMS (Robot Mission Management System) quản lý đội robot giao thuốc, vật tư, mẫu xét nghiệm, gom rác và lau hành lang trong bệnh viện. Tài liệu này mô tả đúng những gì prototype v8 đang làm, để cả nhóm đọc cùng một bản.

## 1. Mục đích và phạm vi

RMMS nhận tổng nhu cầu hàng của các khoa từ file Excel bệnh viện gửi xuống, tự chia thành đơn theo ngăn robot, lập kế hoạch chuyến cho từng robot, phát lệnh qua MQTT tới robot trong Webots, theo dõi robot chạy và xử lý sự cố. Bên cạnh giao hàng theo yêu cầu, hệ thống còn chạy việc định kỳ là gom rác và lau hành lang.

Mô hình dự án: backend .NET, frontend ReactJS, robot mô phỏng trong Webots, giao tiếp robot qua MQTT.

**Trong phạm vi**

- 3 vai trò dùng web: Operator (điều phối viên), Supervisor (giám sát), Administrator (quản trị).
- 5 loại việc: giao thuốc, giao vật tư, chở mẫu xét nghiệm (theo lô), gom rác và lau hành lang (theo lịch).
- 4 loại robot, mỗi robot chỉ chạy trong tầng của mình.
- Import file Excel theo mẫu, kiểm tra từng dòng, tự tách đơn theo sức chứa ngăn.
- Lập kế hoạch bằng AI tối ưu, có thuật toán dự phòng khi AI không phản hồi.
- Xác nhận giao hàng tự động bằng cảm biến ngăn; hết giờ thì ghi giao không thành công.
- Xử lý cảnh báo và sự cố theo tài liệu “Sự cố và luồng xử lý” 05/11/2026.
- Sạc pin tự động, báo cáo vận hành, hiệu suất robot, nhật ký.

**Ngoài phạm vi**

- Không có vai trò nhân viên khoa; khoa không dùng web. Khoa chỉ lấy hàng và bấm nút trên robot.
- Không chở thiết bị lớn (máy thở…), đồ vải bẩn, hàng liên tầng.
- Không quản lý tồn kho, không nối HIS của bệnh viện: file Excel là đầu vào duy nhất.
- Luồng phát hành bản đồ (nhóm sự cố D) chưa dựng trong prototype v8.
## 2. Vai trò và màn hình

Có 3 vai trò và 21 màn hình (SC-xx). Operator làm việc hằng ngày, Supervisor chỉ xem và phân tích, Administrator thiết lập dữ liệu nền.

| Vai trò | Làm gì | Màn hình (theo menu) | Màn mở đầu |
| --- | --- | --- | --- |
| Operator | Tạo nhiệm vụ (đơn lẻ, import Excel), điều phối và phát lệnh, theo dõi chuyến, xử lý cảnh báo và sự cố, quản lý lịch định kỳ | Vận hành: SC-13 Bản đồ trực tiếp, SC-10 Tạo nhiệm vụ, SC-12 Hàng chờ, SC-11 Điều phối, SC-17 Chuyến đang chạy, SC-16 Cảnh báo & sự cố. Kế hoạch: SC-15 Lịch định kỳ, SC-19 Lịch sử nhiệm vụ, SC-18 Trạm sạc (chỉ xem) | SC-13 |
| Supervisor | Xem tổng quan, báo cáo, lịch sử điều phối, hiệu suất robot. Xem mọi màn vận hành ở chế độ chỉ xem | Giám sát: SC-20 Tổng quan, SC-13, SC-17, SC-12, SC-16, SC-15, SC-18. Phân tích: SC-23 Hiệu suất robot, SC-21 Báo cáo vận hành, SC-22 Lịch sử điều phối, SC-19. Hệ thống: SC-07 Nhật ký | SC-20 |
| Administrator | Quản lý người dùng, tầng, bản đồ, điểm dừng, robot và loại robot, danh mục hàng, loại nhiệm vụ và luật | Thiết lập: SC-03 Tầng & bản đồ, SC-04 Vị trí & điểm dừng, SC-05 Robot, SC-08 Danh mục hàng, SC-06 Loại nhiệm vụ & luật. Theo dõi: SC-18 Trạm sạc (sửa số cổng). Hệ thống: SC-02 Người dùng & vai trò, SC-07 Nhật ký | SC-03 |

Màn SC-14 Chi tiết robot mở khi bấm vào một robot. Màn SC-00 Bản đồ màn hình và Tin nhắn MQTT chỉ có trong prototype để demo.

Nội dung chính của các màn Operator:

- SC-10 Tạo nhiệm vụ: 2 tab. “Tạo đơn lẻ” nhập tay một món cho một khoa. “Import file Excel” kéo thả file .xlsx, xem bảng kiểm tra từng dòng, bấm Lưu; có hộp thư file bệnh viện gửi và lịch sử import trong ngày.
- SC-12 Hàng chờ: nhiệm vụ chờ phát lệnh, điểm ưu tiên, Sửa/Huỷ khi còn PENDING, Giao lại/Huỷ cho đơn DELIVERY_FAILED đã về kho.
- SC-11 Điều phối: bấm Lập kế hoạch để xem các chuyến đề xuất; kéo đơn giữa robot, đổi ưu tiên, bỏ một chuyến; một thanh nút duy nhất Bỏ kế hoạch / Lập lại / Phát lệnh N chuyến.
- SC-17 Chuyến đang chạy: từng chuyến, điểm dừng, ngăn nào đã lấy.
- SC-16 Cảnh báo & sự cố: xem tài liệu “RMMS v8 – Xử lý cảnh báo và sự cố”.
- SC-19 Lịch sử nhiệm vụ: tra theo mã nhiệm vụ hoặc mã yêu cầu, xem dòng thời gian từng nhiệm vụ.
## 3. Luồng hoạt động

Một yêu cầu đi qua 8 bước từ file bệnh viện tới lúc khoa lấy hàng. Operator chỉ thao tác ở bước 2 (bấm Lưu) và bước 6 (bấm Phát lệnh); các bước còn lại hệ thống và robot tự làm.

### Luồng 8 bước (chuyển từ sơ đồ trong DOCX sang văn bản)

1. **File Excel:** Bệnh viện gửi tổng nhu cầu từng khoa.
2. **Import, kiểm tra:** Operator bấm Lưu, kiểm tra từng dòng và các lỗi C1, C3.
3. **Chia theo ngăn:** Hệ thống tách đơn theo sức chứa, sinh mã YC-xxxx.
4. **Hàng chờ:** Nhiệm vụ PENDING được tính điểm ưu tiên tăng theo thời gian.
5. **Lập kế hoạch:** AI tối ưu; nếu không phản hồi thì dùng thuật toán dự phòng.
6. **Phát lệnh:** Operator duyệt và gửi order qua MQTT.
7. **Robot lấy hàng:** Robot nạp hàng trong 60 giây, chuyển ASSIGNED → IN_PROGRESS.
8. **Giao tại khoa:** Cảm biến xác nhận ngăn trống → COMPLETED.

**Nhánh sự cố:** Robot gặp sự cố dọc đường → xử lý B1/B2 theo tài liệu sự cố, giao tay hoặc giao lại. Nếu hết giờ khoa không nhận → DELIVERY_FAILED, hàng về kho; Operator có thể Giao lại → RESCHEDULED → quay lại lập kế hoạch tại bước 4.

Hai nhánh lỗi: robot gặp sự cố dọc đường thì xử lý ở màn Cảnh báo & sự cố; khoa không lấy hàng thì đơn thành DELIVERY_FAILED, hàng về kho, và đơn Giao lại quay về bước 4 với trạng thái RESCHEDULED.

## 4. Cách chia hàng vào ngăn

Quy tắc gốc: một ngăn = một đơn (nhiệm vụ) = một mặt hàng của một khoa. Không trộn hàng hay trộn khoa trong một ngăn, để cảm biến so được khối lượng của từng đơn và khoa không lấy nhầm.

Mỗi mặt hàng có một sức chứa tối đa mỗi ngăn (cap) cố định, do Admin đặt ở Danh mục hàng. Một dòng trong file Excel (khoa, món, số lượng q) được chia thành số đơn:

$$
n = \left\lceil \frac{q}{cap} \right\rceil
$$

Các ngăn đầy trước, ngăn cuối chứa phần lẻ. Tất cả đơn tách ra từ một dòng chung một mã yêu cầu YC-xxxx.

| Dòng trong file | Sức chứa mỗi ngăn | Kết quả |
| --- | --- | --- |
| Hồi sức tích cực cần NaCl 0,9% 500 ml × 20 chai | 8 chai | 3 đơn: 8 + 8 + 4 |
| Khoa Nội A cần Amoxicillin 500 mg × 45 hộp | 30 hộp | 2 đơn: 30 + 15 |
| Khoa Nội A cần Gạc vô khuẩn × 100 gói | 80 gói | 2 đơn: 80 + 20 |
| Hồi sức cần Noradrenalin × 30 ống | 50 ống | 1 đơn: 30 |

Cách chọn sức chứa: lấy theo thể tích hoặc khối lượng, cái nào chạm trước, hoặc theo khay chuyên dụng (khay 60 lọ, giá 40 ống máu). Luôn có cap × khối lượng một đơn vị ≤ tải mỗi ngăn của robot. Vì số ngăn × tải mỗi ngăn ≤ tải trọng robot, lúc lập kế hoạch chỉ cần đếm ngăn, không cần cộng khối lượng (BR-11).

Giới hạn khác khi import: mỗi dòng tối đa 600 đơn vị (hơn thì tách thành nhiều dòng); món không có trong danh mục thì không tạo nhiệm vụ (C3); tầng của khoa phải có điểm lấy cho loại việc đó (BR-04).

### File import

File là Excel .xlsx theo mẫu “RMMS-YC v3”, không dùng CSV. Lý do: kho và khoa đã quen Excel; mẫu khoá cột, có danh sách chọn cho mã khoa, mã hàng, ưu tiên nên ít gõ sai; giữ được tiếng Việt và định dạng giờ mà CSV hay làm hỏng; có sheet ẩn _RMMS ghi phiên bản mẫu để hệ thống cảnh báo khi dùng mẫu cũ.

| Cột | Tên cột trong file | Bắt buộc | Ví dụ |
| --- | --- | --- | --- |
| Mã yêu cầu | request_code | Không (trống thì hệ thống tự cấp) | YC-0101 |
| Khoa nhận | department | Có | F2-ICU |
| Mã hàng | item_code | Có | TH-NACL |
| Số lượng | quantity | Có, số nguyên 1–600 | 20 |
| Ưu tiên | priority | Có: Thường / Cao / Khẩn | Cao |
| Hạn giao | deadline | Không, dạng HH:MM | 10:30 |
| Ghi chú | note | Không | Dịch truyền |

## 5. Các loại robot

Có 4 loại robot. Hai loại có ngăn chứa chở hàng theo lô; hai loại không có ngăn chạy theo lịch. Số liệu là giả định của prototype, cần nhóm và bệnh viện xác nhận.

| Loại robot | Mã | Việc làm | Ngăn chứa | Tải mỗi ngăn | Tải trọng tối đa | Tốc độ | Nhà / trạm sạc |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Xe thuốc | MED_CART | Giao thuốc, chở mẫu xét nghiệm | 4 ngăn, 40 × 30 × 20 cm, 24 L | ≤ 5 kg | 20 kg (4 × 5) | 1,0 m/s | Trạm sạc cạnh phòng thuốc |
| Xe vật tư | SUPPLY_CART | Giao vật tư y tế | 3 ngăn, 60 × 40 × 30 cm, 72 L | ≤ 10 kg | 30 kg (3 × 10) | 0,7 m/s | Trạm sạc cạnh kho vật tư |
| Xe gom rác | WASTE_CART | Gom rác theo lịch (không gom đồ vải bẩn) | Không có ngăn, 1 thùng kín | — | 40 kg | 0,7 m/s | Trạm sạc cạnh trạm rác |
| Robot lau hành lang | CLEAN_BOT | Lau hành lang theo lịch | Không có ngăn, 1 bình nước | — | 12 L | 0,5 m/s | Trạm CLEAN (chỉ loại này dùng) |

Mỗi ngăn của xe thuốc và xe vật tư có cảm biến khối lượng (sai số ±20 g) và công tắc cửa. Robot có thêm một nút vật lý “Đã lấy hàng” cho khoa bấm.

Luật về robot:

- Robot chỉ chạy trong tầng của mình, không đi thang máy.
- Mỗi loại việc chỉ dùng đúng một loại robot (BR-13).
- Robot nhận chuyến khi đang rảnh, còn kết nối, không bị tạm ngưng và pin từ 20% trở lên.
- Trạng thái robot: Sẵn sàng, Đang chạy, Dừng tại điểm, Về trạm, Đang sạc, Mất kết nối, Báo lỗi, Bảo trì, Chờ kết nối, Tạm ngưng.
Đội robot mẫu trong prototype có 9 robot:

| Tầng | Xe thuốc | Xe vật tư | Xe gom rác | Robot lau |
| --- | --- | --- | --- | --- |
| Tầng 2 – Khối Nội | MED-01, MED-02 | SUP-01 | WASTE-01 | CLEAN-01 |
| Tầng 3 – Khối Ngoại | MED-03 | SUP-02 | WASTE-02 | CLEAN-02 |

## 6. Hàng vận chuyển và loại nhiệm vụ

Danh mục có 16 mặt hàng: 7 thuốc, 3 mẫu xét nghiệm, 6 vật tư. Admin sửa ở màn SC-08 Danh mục hàng; file mẫu Excel tự cập nhật theo danh mục.

| Mã hàng | Mặt hàng | Loại việc | Đóng gói | Khối lượng mỗi đơn vị (kg) | Tối đa mỗi ngăn | Giới hạn bởi |
| --- | --- | --- | --- | --- | --- | --- |
| TH-PARA | Paracetamol 500 mg | Giao thuốc | hộp 10 vỉ × 10 viên | 0,10 | 30 hộp | Thể tích ngăn |
| TH-AMOX | Amoxicillin 500 mg | Giao thuốc | hộp 10 vỉ × 10 viên | 0,12 | 30 hộp | Thể tích ngăn |
| TH-CEFT | Ceftriaxone 1 g (tiêm) | Giao thuốc | lọ bột pha tiêm | 0,03 | 60 lọ | Khay chia ô 60 lọ |
| TH-NACL | NaCl 0,9% 500 ml | Giao thuốc | chai nhựa 500 ml | 0,55 | 8 chai | Khối lượng (≤ 5 kg/ngăn) |
| TH-INSU | Insulin bút tiêm | Giao thuốc | bút 3 ml, kèm túi giữ lạnh | 0,04 | 20 bút | Túi giữ lạnh chiếm chỗ |
| TH-NORA | Noradrenalin 1 mg/ml | Giao thuốc | ống thuỷ tinh 4 ml | 0,01 | 50 ống | Khay chống sốc 50 ô |
| TH-SALB | Salbutamol khí dung | Giao thuốc | hộp 20 ống 2,5 ml | 0,15 | 20 hộp | Thể tích ngăn |
| XN-MAU | Ống mẫu máu | Chở mẫu xét nghiệm | ống 4 ml có nắp | 0,01 | 40 ống | Giá đỡ 40 lỗ |
| XN-KHIMAU | Ống khí máu (kèm đá) | Chở mẫu xét nghiệm | ống trong túi đá | 0,15 | 10 ống | Hộp giữ lạnh 10 ô |
| XN-NTIEU | Lọ nước tiểu | Chở mẫu xét nghiệm | lọ 60 ml có nắp | 0,08 | 20 lọ | Giá đỡ 20 lỗ |
| VT-GANG-S | Găng tay khám size S | Giao vật tư | hộp 100 chiếc | 0,55 | 15 hộp | Thể tích ngăn |
| VT-GANG-M | Găng tay khám size M | Giao vật tư | hộp 100 chiếc | 0,55 | 15 hộp | Thể tích ngăn |
| VT-GAC | Gạc vô khuẩn 10 × 10 cm | Giao vật tư | gói 10 miếng | 0,05 | 80 gói | Thể tích ngăn |
| VT-BOM5 | Bơm tiêm 5 ml | Giao vật tư | hộp 100 cái | 0,90 | 10 hộp | Khối lượng (≤ 10 kg/ngăn) |
| VT-TBANG | Bộ dụng cụ thay băng | Giao vật tư | bộ tiệt khuẩn đóng túi | 0,40 | 20 bộ | Thể tích ngăn |
| VT-KTRANG | Khẩu trang y tế | Giao vật tư | hộp 50 cái | 0,25 | 24 hộp | Thể tích ngăn |

### Loại nhiệm vụ

Có 5 loại nhiệm vụ. Ba loại “theo lô” đến từ file Excel hoặc đơn lẻ; hai loại “theo lịch” sinh từ lịch định kỳ.

| Loại nhiệm vụ | Mã | Cách tạo | Robot | Điểm lấy | Thời gian chờ khoa lấy hàng | Vì sao |
| --- | --- | --- | --- | --- | --- | --- |
| Giao thuốc | MEDICINE_DELIVERY | Theo lô | Xe thuốc | Phòng thuốc cùng tầng | 3 phút | Thuốc theo y lệnh, khoa thường có điều dưỡng ở quầy |
| Giao vật tư | SUPPLY_DELIVERY | Theo lô | Xe vật tư | Kho vật tư cùng tầng | 5 phút | Vật tư không gấp, khoa có thể đang bận |
| Chở mẫu xét nghiệm | SPECIMEN_TRANSPORT | Theo lô | Xe thuốc | Phòng thu mẫu, giao tới phòng xét nghiệm cùng tầng | 2 phút | Mẫu để lâu bị hỏng |
| Gom rác | WASTE_COLLECTION | Theo lịch | Xe gom rác | — (đi qua mọi điểm gom rác rồi về trạm rác) | Không có người nhận | — |
| Lau hành lang | CORRIDOR_CLEANING | Theo lịch | Robot lau | — (đi qua mọi đoạn hành lang) | Không có người nhận | — |

Món không có trong danh mục (thiết bị lớn như máy thở, đồ vải bẩn) thì không chở bằng robot; kho cho người vận chuyển.

## 7. Lập kế hoạch và phát lệnh

Hệ thống đề xuất, Operator quyết: robot chỉ chạy sau khi Operator bấm Phát lệnh. Kế hoạch tính riêng từng tầng, chỉ dùng robot đang rảnh, còn kết nối, không tạm ngưng, pin từ 20%.

### Luật ghép đơn vào một chuyến

Một đơn chỉ vào được chuyến của robot khi đủ 4 điều kiện:

- Đúng loại robot của loại việc (BR-13).
- Cùng tầng với robot.
- Còn ngăn trống: số đơn trong chuyến ≤ số ngăn (BR-11).
- Cùng điểm lấy với các đơn đã có trong chuyến (BR-10). Ví dụ xe thuốc không chở cùng lúc thuốc từ phòng thuốc và mẫu từ phòng lấy máu.
Một chuyến: robot tới điểm lấy, dừng 60 giây để kho nạp hàng vào ngăn, đi lần lượt các khoa, rồi về trạm. Nhiều ngăn cùng một khoa thì robot dừng một lần.

### AI tối ưu (mặc định)

Mục tiêu, theo thứ tự ưu tiên:

- Ít trễ hạn nhất: tổng số phút trễ so với hạn giao (cho 60 giây ân hạn), nhân trọng số ưu tiên: Thường 1, Cao 3, Khẩn 6.
- Nếu bằng nhau: xong sớm nhất = tổng thời gian hoàn thành có trọng số + β × quãng đường (β = 0,005 phút cho mỗi mét).
Cách tính: xếp đơn theo hạn giao rồi theo ưu tiên, lần lượt thử đưa từng đơn vào mỗi robot hợp lệ và giữ phương án tốt nhất; sau đó thử đổi chỗ đơn giữa các robot tối đa 30 vòng; mỗi chuyến chọn thứ tự ghé khoa tốt nhất. Màn Điều phối so kết quả với cách xếp đơn giản (theo thứ tự nhập, lấp robot lần lượt) và giải thích đơn nào dự kiến trễ, vì sao.

### Thuật toán dự phòng (khi AI không phản hồi, E3)

Gom đơn theo điểm lấy, xếp theo điểm ưu tiên giảm dần, giao cho robot rảnh gần điểm lấy nhất, lấp ngăn theo thứ tự (BR-15).

### Điểm ưu tiên trong hàng chờ

| Thành phần | Cách tính |
| --- | --- |
| Mức ưu tiên | Thường 10, Cao 30, Khẩn 60 |
| Hạn giao | Còn ≤ 30 phút: +25; ≤ 60 phút: +15; ≤ 120 phút: +5 |
| Thời gian chờ | +1 mỗi 3 phút, tối đa +20 |
| Đơn giao lại (RESCHEDULED) | +15 |

### Thao tác của Operator trên màn Điều phối

- Lập kế hoạch: xem các chuyến đề xuất, đơn còn phải chờ và lý do.
- Chuyển đơn sang robot khác (hệ thống kiểm tra lại 4 luật trên), đổi ưu tiên, Bỏ chuyến này.
- Phát lệnh N chuyến: hệ thống kiểm tra lại robot ngay lúc phát; robot nào không còn sẵn sàng thì tự lập lại kế hoạch và báo Operator xem lại. Sau khi phát, kế hoạch chỉ đổi được qua xử lý sự cố (BR-14).
- Tự soạn: khi một robot vừa rảnh mà còn đơn chờ đúng loại, hệ thống tự soạn kế hoạch; Operator chỉ cần bấm Phát lệnh. Nếu kế hoạch đang có chỉnh tay thì chỉ nhắc, không ghi đè.
## 8. Giao hàng bằng cảm biến và trạng thái nhiệm vụ

Không có ai xác nhận trên web: robot tự xác nhận bằng cảm biến ngăn, hoặc tự ghi giao không thành công khi hết thời gian chờ.

Trình tự tại một khoa:

- Robot tới khoa, mở các ngăn của khoa đó, bắt đầu đếm thời gian chờ theo loại việc (thuốc 3 phút, vật tư 5 phút, mẫu 2 phút).
- Nhân viên khoa lấy hàng, đóng cửa. Khi cảm biến đọc về 0 kg (±20 g) và cửa đã đóng, robot gửi sự kiện COMPARTMENT_EMPTY; nhiệm vụ của ngăn đó thành COMPLETED.
- Các ngăn của khoa đã trống:
- Có người bấm nút “Đã lấy hàng”: robot đi ngay.
- Không ai bấm: robot tự đi sau 15 giây (A2).
- Bấm nút khi ngăn còn hàng: robot không đi, báo tại chỗ (A3).
- Hết thời gian chờ mà ngăn còn hàng: cảnh báo A1. Operator chọn chờ thêm hoặc cho đi tiếp (không phản hồi 2 phút thì robot tự đi). Nhiệm vụ còn hàng thành DELIVERY_FAILED; robot giao nốt rồi mang hàng về điểm lấy.
Giờ giao tính từ lúc robot tới khoa, nên thời gian khoa ra lấy hàng không bị tính là trễ.

### Trạng thái nhiệm vụ

| Trạng thái | Nghĩa | Sang trạng thái tiếp |
| --- | --- | --- |
| PENDING | Mới tạo, nằm trong hàng chờ | ASSIGNED khi phát lệnh; CANCELLED nếu Operator huỷ |
| RESCHEDULED | Về lại hàng chờ sau sự cố hoặc sau khi Giao lại, được +15 điểm ưu tiên | Như PENDING |
| ASSIGNED | Đã phát lệnh, robot đang tới điểm lấy | IN_PROGRESS khi hàng lên robot |
| IN_PROGRESS | Hàng đã trong ngăn, robot đang đi giao | COMPLETED, COMPLETED_BY_HUMAN hoặc DELIVERY_FAILED |
| COMPLETED | Cảm biến báo ngăn trống | Kết thúc |
| COMPLETED_BY_HUMAN | Người giao tay sau sự cố (B1, B2, A4) | Kết thúc |
| DELIVERY_FAILED | Hết giờ ngăn còn hàng, hàng mang về kho | RESCHEDULED (Giao lại) hoặc CANCELLED (Huỷ) |
| CANCELLED | Đã huỷ | Kết thúc |

Nhiệm vụ đã phát lệnh không huỷ được. Chỉ huỷ được khi còn PENDING (C4) hoặc khi đã DELIVERY_FAILED và hàng đã về kho (A1). Chi tiết từng sự cố xem tài liệu RMMS v8 – Xử lý cảnh báo và sự cố.

## 9. Pin, trạm sạc và lịch định kỳ

### Pin

| Thông số | Giá trị giả định |
| --- | --- |
| Hao pin khi chạy | 0,72% mỗi phút (khoảng 139 phút từ 100% về 0) |
| Tốc độ sạc | 3% mỗi phút; từ 20% lên 90% mất khoảng 23 phút |
| Ngưỡng không nhận chuyến | Dưới 20%: không nhận chuyến, tự về sạc (NSF-08) |
| Sạc xong | 90% thì rời cổng, sẵn sàng nhận chuyến |
| Pin yếu giữa chuyến | 15%: cảnh báo, chạy nốt rồi về sạc; dưới 10%: coi là sự cố (B6, chờ chốt) |

### Trạm sạc

Mỗi tầng có 4 trạm: cạnh phòng thuốc (2 cổng), cạnh kho vật tư (1 cổng), cạnh trạm rác (1 cổng), trạm CLEAN (1 cổng, chỉ cho robot lau). Admin sửa số cổng ở màn Trạm sạc.

Robot về trạm nhà trước. Trạm nhà đầy thì sang trạm khác cùng tầng; hết cổng thì đứng chờ với nhãn “chờ cổng sạc”, có cổng trống thì tự vào (E2, chưa chốt). Màn SC-18 hiện cổng đang dùng, robot đang sạc và hàng chờ sạc.

### Lịch định kỳ

Gom rác và lau hành lang không đi qua điều phối. Operator tạo lịch (loại việc, tầng, giờ, lặp lại) ở màn SC-15; tới giờ hệ thống tự giao cho robot đúng loại của tầng đó.

- Gom rác: đi qua mọi điểm gom rác của tầng (5 điểm mỗi tầng) rồi về trạm rác.
- Lau hành lang: đi qua mọi đoạn hành lang của tầng (4 đoạn mỗi tầng).
- Robot đúng loại đang bận: lịch chờ robot rảnh; chờ quá 15 phút thì cảnh báo E1.
Lịch mẫu: LD-01 gom rác tầng 2 lúc 07:00, LD-02 gom rác tầng 2 lúc 14:00, LD-03 lau hành lang tầng 2 lúc 09:12, LD-04 gom rác tầng 3 lúc 09:20; tất cả lặp hằng ngày.

## 10. Tầng, vị trí và dữ liệu mẫu

Prototype mô phỏng 2 tầng, mỗi tầng một hành lang chính, phòng ban nằm hai bên. Mỗi vị trí có mã dạng `<tầng>-<mã>`; file Excel dùng đúng mã này ở cột Khoa nhận.

| Tầng | Điểm lấy hàng | Khoa / phòng nhận hàng | Điểm khác |
| --- | --- | --- | --- |
| F2 – Tầng 2, Khối Nội | Phòng thuốc (F2-PHARM), Kho vật tư (F2-STORE), Phòng lấy máu (F2-BLOOD) | Khoa Nội A (F2-WARD-A), Khoa Nội B (F2-WARD-B), Khoa Nhi (F2-PEDS), Hồi sức tích cực (F2-ICU), Khoa Tim mạch (F2-CARD), Phòng xét nghiệm (F2-LAB), Khoa Ngoại tổng hợp (F2-SURG), Khoa Hô hấp (F2-RESP) | Trạm rác, 5 điểm gom rác, 4 đoạn hành lang, 3 trạm sạc, 1 trạm CLEAN |
| F3 – Tầng 3, Khối Ngoại | Phòng thuốc (F3-PHARM), Kho vật tư (F3-STORE) | Khoa Ngoại A (F3-SURG-A), Khoa Ngoại B (F3-SURG-B), Khoa Sản (F3-OBS), Tai Mũi Họng (F3-ENT), Chấn thương chỉnh hình (F3-ORTHO), Phòng mổ (F3-OR), Phòng hồi tỉnh (F3-RECOV), Phòng nội soi (F3-ENDO), Chẩn đoán hình ảnh (F3-XRAY) | Trạm rác, 5 điểm gom rác, 4 đoạn hành lang, 3 trạm sạc, 1 trạm CLEAN |

Tầng 3 không có phòng thu mẫu, nên việc chở mẫu xét nghiệm chỉ chạy ở tầng 2 (từ Phòng lấy máu tới Phòng xét nghiệm).

Loại vị trí: Phòng thuốc, Kho vật tư, Phòng thu mẫu (3 loại điểm lấy), Khoa/phòng nhận, Trạm rác, Điểm gom rác, Trạm sạc, Trạm CLEAN, Đoạn hành lang.

Người dùng mẫu: 3 Operator (1 đang khoá), 1 Supervisor, 1 Administrator. Prototype có đồng hồ mô phỏng riêng, bắt đầu buổi sáng với sẵn vài lô import và chuyến đã chạy để màn hình có dữ liệu.

## 11. Giao tiếp với robot qua MQTT

Backend và robot trong Webots nói chuyện qua MQTT broker, 4 topic cho mỗi robot. Robot gửi heartbeat qua topic state; mất quá 5 giây thì coi là mất kết nối (B5). Màn “Tin nhắn MQTT” trong prototype hiện từng tin để nhóm làm robot đối chiếu.

| Topic | Chiều | Nội dung |
| --- | --- | --- |
| rmms/v1/{robotId}/order | Backend → robot | Chuyến mới: danh sách điểm dừng (PICKUP, DROP) và ngăn nào chứa nhiệm vụ nào. Hoặc thêm điểm RETURN để mang hàng giao hỏng về kho |
| rmms/v1/{robotId}/command | Backend → robot | Lệnh lẻ, ví dụ CLOSE_AND_CONTINUE (khoá ngăn còn hàng và đi tiếp, lý do NO_RECIPIENT hoặc ALL_TAKEN) |
| rmms/v1/{robotId}/state | Robot → backend | Trạng thái định kỳ (status, toạ độ x, y, pin) và sự kiện |
| rmms/v1/{robotId}/alert | Robot → backend | Báo lỗi: loại, mức độ, chuyến đang chạy, nhiệm vụ bị ảnh hưởng |

Các sự kiện robot gửi qua state:

| Sự kiện | Khi nào | Trường chính |
| --- | --- | --- |
| ARRIVED | Tới một khoa và mở ngăn | orderId, seq, nodeId, openCompartments (slot, taskId, weightKg), waitSec |
| COMPARTMENT_EMPTY | Ngăn về 0 kg và cửa đã đóng | slot, taskId, weightBeforeKg, weightAfterKg, doorClosed; có thêm expectedNode/actualNode khi lấy nhầm (A4) |
| BUTTON_PRESSED | Bấm nút khi ngăn còn hàng (A3) | slotsNotEmpty, action: STAY |
| STOP_COMPLETED | Rời một điểm dừng | orderId, seq, nodeId, battery |
| status IDLE / CHARGING | Về trạm, vào sạc | nodeId hoặc stationId, battery |

Ví dụ một tin order:

```json
{
  "orderId": "CH-021",
  "robotId": "MED-01",
  "stops": [
    {"seq": 1, "nodeId": "F2-PHARM", "action": "PICKUP", "taskIds": ["NV-101", "NV-102", "NV-103"]},
    {"seq": 2, "nodeId": "F2-ICU", "action": "DROP", "taskIds": ["NV-101", "NV-102"]},
    {"seq": 3, "nodeId": "F2-PEDS", "action": "DROP", "taskIds": ["NV-103"]}
  ],
  "compartments": [
    {"slot": 1, "taskId": "NV-101", "item": "TH-NACL", "qty": 8},
    {"slot": 2, "taskId": "NV-102", "item": "TH-NACL", "qty": 4},
    {"slot": 3, "taskId": "NV-103", "item": "TH-PARA", "qty": 20}
  ]
}
```

## 12. Giả định cần nhóm chốt

Các số liệu dưới đây do prototype tự đặt để chạy được. Cần nhóm (và nếu được, bệnh viện) xác nhận trước khi đưa vào backend.

- [ ] Kích thước, số ngăn, tải mỗi ngăn và tốc độ của 4 loại robot (mục 5); khớp với model robot trong Webots chưa.
- [ ] Danh mục 16 mặt hàng và sức chứa tối đa mỗi ngăn (mục 6).
- [ ] Thời gian chờ khoa lấy hàng: thuốc 3 phút, vật tư 5 phút, mẫu 2 phút.
- [ ] Robot tự đi 15 giây sau khi ngăn trống mà không ai bấm nút (A2); chờ Operator 2 phút sau A1.
- [ ] Thời gian dừng ở điểm lấy 60 giây để nạp hàng.
- [ ] Hao pin 0,72%/phút, sạc 3%/phút, ngưỡng 20% và 90%, số cổng mỗi trạm sạc.
- [ ] Trọng số ưu tiên (1/3/6), β = 0,005, bảng điểm ưu tiên hàng chờ (mục 7).
- [ ] File import dùng Excel .xlsx theo mẫu “RMMS-YC v3” thay CSV; mỗi dòng tối đa 600 đơn vị.
- [ ] Robot không đi liên tầng; tầng 3 không chở mẫu xét nghiệm.
- [ ] Các điểm sự cố còn mở (A1, A4, A5, B4–B7, C5, D5, E1–E4): xem mục 7 của tài liệu xử lý sự cố.
