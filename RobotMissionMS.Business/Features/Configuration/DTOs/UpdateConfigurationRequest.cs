using System.ComponentModel.DataAnnotations;

namespace RobotMissionMS.Business.Features.Configuration.DTOs;

public sealed class UpdateConfigurationRequest
{
    [Range(0, 100)] public int? MinDispatchBatteryPercent { get; init; }
    [Range(0, 100)] public int? ChargeCompleteBatteryPercent { get; init; }
    [Range(0, 100)] public int? WarningBatteryPercent { get; init; }
    [Range(0, 100)] public int? CriticalBatteryPercent { get; init; }
    [Range(1, 3600)] public int? PickupLoadingSeconds { get; init; }
    [Range(1, 3600)] public int? HeartbeatTimeoutSeconds { get; init; }
    [Range(1, 1440)] public int? ScheduleMaxWaitMinutes { get; init; }
    [Range(0, 100000)] public int? NormalPriorityPoints { get; init; }
    [Range(0, 100000)] public int? HighPriorityPoints { get; init; }
    [Range(0, 100000)] public int? UrgentPriorityPoints { get; init; }
    [Range(1, 1440)] public int? QueueWaitStepMinutes { get; init; }
    [Range(0, 100000)] public int? QueueMaxWaitBonus { get; init; }
    [Range(0, 100000)] public int? QueueRedeliveryBonus { get; init; }
    [Range(typeof(decimal), "0", "100000")] public decimal? SchedulerNormalWeight { get; init; }
    [Range(typeof(decimal), "0", "100000")] public decimal? SchedulerHighWeight { get; init; }
    [Range(typeof(decimal), "0", "100000")] public decimal? SchedulerUrgentWeight { get; init; }
    [Range(typeof(decimal), "0.000001", "100000")] public decimal? SchedulerDistanceBeta { get; init; }
    [Range(0, 3600)] public int? SchedulerGraceSeconds { get; init; }
}
