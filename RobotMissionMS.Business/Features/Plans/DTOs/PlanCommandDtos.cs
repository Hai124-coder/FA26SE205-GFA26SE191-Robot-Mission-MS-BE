using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Plans.DTOs;

public sealed class PlanTaskAssignmentRequest { public Guid? RobotId { get; init; } }
public sealed class PlanTaskPriorityRequest { [Required, StringLength(20)] public string? Priority { get; init; } }
