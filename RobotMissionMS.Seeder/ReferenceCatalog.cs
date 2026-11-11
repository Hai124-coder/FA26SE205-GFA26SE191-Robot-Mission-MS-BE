using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.Seeder;

internal static class ReferenceCatalog
{
    internal const string Marker = "RMMS development reference seed v1; prototype assumptions, not approved production data.";

    internal static readonly (string Code, string Name, string Mission, string Unit, int Capacity)[] Items =
    [
        ("TH-PARA", "Paracetamol 500 mg", "MEDICINE_DELIVERY", "hộp", 30),
        ("TH-AMOX", "Amoxicillin 500 mg", "MEDICINE_DELIVERY", "hộp", 30),
        ("TH-CEFT", "Ceftriaxone 1 g", "MEDICINE_DELIVERY", "lọ", 60),
        ("TH-NACL", "NaCl 0,9% 500 ml", "MEDICINE_DELIVERY", "chai", 8),
        ("TH-INSU", "Insulin bút tiêm", "MEDICINE_DELIVERY", "bút", 20),
        ("TH-NORA", "Noradrenalin 1 mg/ml", "MEDICINE_DELIVERY", "ống", 50),
        ("TH-SALB", "Salbutamol khí dung", "MEDICINE_DELIVERY", "hộp", 20),
        ("XN-MAU", "Ống mẫu máu", "SPECIMEN_TRANSPORT", "ống", 40),
        ("XN-KHIMAU", "Ống khí máu (kèm đá)", "SPECIMEN_TRANSPORT", "ống", 10),
        ("XN-NTIEU", "Lọ nước tiểu", "SPECIMEN_TRANSPORT", "lọ", 20),
        ("VT-GANG-S", "Găng tay khám size S", "SUPPLY_DELIVERY", "hộp", 15),
        ("VT-GANG-M", "Găng tay khám size M", "SUPPLY_DELIVERY", "hộp", 15),
        ("VT-GAC", "Gạc vô khuẩn 10 × 10 cm", "SUPPLY_DELIVERY", "gói", 80),
        ("VT-BOM5", "Bơm tiêm 5 ml", "SUPPLY_DELIVERY", "hộp", 10),
        ("VT-TBANG", "Bộ dụng cụ thay băng", "SUPPLY_DELIVERY", "bộ", 20),
        ("VT-KTRANG", "Khẩu trang y tế", "SUPPLY_DELIVERY", "hộp", 24)
    ];

    internal static IEnumerable<(string Code, string Name, LocationKind Kind)> Locations(string floor)
    {
        yield return ($"{floor}-PHARM", "Phòng thuốc", LocationKind.Pharmacy);
        yield return ($"{floor}-STORE", "Kho vật tư", LocationKind.Storage);
        if (floor == "F2") yield return ("F2-BLOOD", "Phòng lấy máu", LocationKind.Specimen);
        var departments = floor == "F2"
            ? new[] { ("WARD-A", "Khoa Nội A"), ("WARD-B", "Khoa Nội B"), ("PEDS", "Khoa Nhi"), ("ICU", "Hồi sức tích cực"), ("CARD", "Khoa Tim mạch"), ("LAB", "Phòng xét nghiệm"), ("SURG", "Khoa Ngoại tổng hợp"), ("RESP", "Khoa Hô hấp") }
            : new[] { ("SURG-A", "Khoa Ngoại A"), ("SURG-B", "Khoa Ngoại B"), ("OBS", "Khoa Sản"), ("ENT", "Tai Mũi Họng"), ("ORTHO", "Chấn thương chỉnh hình"), ("OR", "Phòng mổ"), ("RECOV", "Phòng hồi tỉnh"), ("ENDO", "Phòng nội soi"), ("XRAY", "Chẩn đoán hình ảnh") };
        foreach (var (code, name) in departments) yield return ($"{floor}-{code}", name, LocationKind.Dept);
        yield return ($"{floor}-WASTE", "Trạm rác", LocationKind.WasteStation);
        for (var i = 1; i <= 5; i++) yield return ($"{floor}-WASTE-{i:00}", $"Điểm gom rác {i}", LocationKind.WastePoint);
        for (var i = 1; i <= 4; i++) yield return ($"{floor}-CORRIDOR-{i:00}", $"Đoạn hành lang {i}", LocationKind.Corridor);
        yield return ($"{floor}-CHARGE-MED", "Trạm sạc xe thuốc", LocationKind.Charger);
        yield return ($"{floor}-CHARGE-SUP", "Trạm sạc xe vật tư", LocationKind.Charger);
        yield return ($"{floor}-CHARGE-WASTE", "Trạm sạc xe gom rác", LocationKind.Charger);
        yield return ($"{floor}-CLEAN", "Trạm robot lau", LocationKind.CleanStation);
    }

    // Proposed application keys; future services must explicitly consume these keys.
    internal static readonly (string Key, string Value)[] Settings =
    [
        ("Battery.MinDispatchPercent", "20"), ("Battery.ChargeCompletePercent", "90"),
        ("Battery.WarningPercent", "15"), ("Battery.CriticalPercent", "10"),
        ("Pickup.LoadingSeconds", "60"), ("Robot.HeartbeatTimeoutSeconds", "5"),
        ("Incident.OperatorWaitSeconds", "120"), ("Schedule.MaxWaitMinutes", "15"),
        ("Queue.NormalPriority", "10"), ("Queue.HighPriority", "30"), ("Queue.UrgentPriority", "60"),
        ("Queue.WaitStepMinutes", "3"), ("Queue.MaxWaitBonus", "20"), ("Queue.RedeliveryBonus", "15"),
        ("Scheduler.NormalWeight", "1"), ("Scheduler.HighWeight", "3"), ("Scheduler.UrgentWeight", "6"),
        ("Scheduler.DistanceBeta", "0.005"), ("Scheduler.GraceSeconds", "60"), ("Import.MaxQuantityPerRow", "600")
    ];
}
