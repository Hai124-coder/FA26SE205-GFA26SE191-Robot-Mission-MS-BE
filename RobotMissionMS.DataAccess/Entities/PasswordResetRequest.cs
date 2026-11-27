using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.DataAccess.Entities;

public sealed class PasswordResetRequest
{
    public Guid Id { get; set; }
    public string RequestCode { get; set; } = null!;
    public Guid UserId { get; set; }
    public string? Note { get; set; }
    public PasswordResetRequestStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public Guid? ProcessedByUserId { get; set; }
    public User User { get; set; } = null!;
    public User? ProcessedByUser { get; set; }
}
