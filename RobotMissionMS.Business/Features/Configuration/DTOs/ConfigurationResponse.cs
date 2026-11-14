namespace RobotMissionMS.Business.Features.Configuration.DTOs;

public sealed class ConfigurationResponse
{
    public required int MinDispatchBatteryPercent { get; init; }
    public required int ChargeCompleteBatteryPercent { get; init; }
    public required int WarningBatteryPercent { get; init; }
    public required int CriticalBatteryPercent { get; init; }
    public required int PickupLoadingSeconds { get; init; }
    public required int HeartbeatTimeoutSeconds { get; init; }
    public required int ScheduleMaxWaitMinutes { get; init; }
    public required int NormalPriorityPoints { get; init; }
    public required int HighPriorityPoints { get; init; }
    public required int UrgentPriorityPoints { get; init; }
    public required int QueueWaitStepMinutes { get; init; }
    public required int QueueMaxWaitBonus { get; init; }
    public required int QueueRedeliveryBonus { get; init; }
    public required decimal SchedulerNormalWeight { get; init; }
    public required decimal SchedulerHighWeight { get; init; }
    public required decimal SchedulerUrgentWeight { get; init; }
    public required decimal SchedulerDistanceBeta { get; init; }
    public required int SchedulerGraceSeconds { get; init; }
}
