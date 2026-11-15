using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.DataAccess.Entities;

public partial class BatchRow
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public int RowNo { get; set; }
    public string? RequestCode { get; set; }
    public string DepartmentCode { get; set; } = null!;
    public string ItemCode { get; set; } = null!;
    public int? Quantity { get; set; }
    public MissionPriority? Priority { get; set; }
    public string? DeadlineText { get; set; }
    public string? Note { get; set; }
    public Guid? ZoneId { get; set; }
    public Guid? DepartmentLocationId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? MissionTypeId { get; set; }
    public bool IsValid { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? WarningMessage { get; set; }
    public string SplitQuantities { get; set; } = "[]";
    public int MissionsToCreate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public virtual Batch Batch { get; set; } = null!;
    public virtual Zone? Zone { get; set; }
    public virtual Location? DepartmentLocation { get; set; }
    public virtual Item? Item { get; set; }
    public virtual MissionType? MissionType { get; set; }
}
