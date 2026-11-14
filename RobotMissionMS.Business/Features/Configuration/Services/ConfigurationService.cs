using System.ComponentModel.DataAnnotations;
using System.Globalization;
using RobotMissionMS.Business.Features.Configuration.DTOs;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.Configuration.Services;

public sealed class ConfigurationService(IConfigurationRepository repository) : IConfigurationService
{
    private static class Keys
    {
        internal const string MinDispatchBattery = "Battery.MinDispatchPercent";
        internal const string ChargeCompleteBattery = "Battery.ChargeCompletePercent";
        internal const string WarningBattery = "Battery.WarningPercent";
        internal const string CriticalBattery = "Battery.CriticalPercent";
        internal const string PickupLoading = "Pickup.LoadingSeconds";
        internal const string Heartbeat = "Robot.HeartbeatTimeoutSeconds";
        internal const string ScheduleMaxWait = "Schedule.MaxWaitMinutes";
        internal const string NormalPriority = "Queue.NormalPriority";
        internal const string HighPriority = "Queue.HighPriority";
        internal const string UrgentPriority = "Queue.UrgentPriority";
        internal const string QueueWaitStep = "Queue.WaitStepMinutes";
        internal const string QueueMaxWaitBonus = "Queue.MaxWaitBonus";
        internal const string QueueRedeliveryBonus = "Queue.RedeliveryBonus";
        internal const string SchedulerNormalWeight = "Scheduler.NormalWeight";
        internal const string SchedulerHighWeight = "Scheduler.HighWeight";
        internal const string SchedulerUrgentWeight = "Scheduler.UrgentWeight";
        internal const string SchedulerDistanceBeta = "Scheduler.DistanceBeta";
        internal const string SchedulerGrace = "Scheduler.GraceSeconds";

        internal static readonly string[] All = [MinDispatchBattery, ChargeCompleteBattery, WarningBattery, CriticalBattery,
            PickupLoading, Heartbeat, ScheduleMaxWait, NormalPriority, HighPriority, UrgentPriority, QueueWaitStep,
            QueueMaxWaitBonus, QueueRedeliveryBonus, SchedulerNormalWeight, SchedulerHighWeight, SchedulerUrgentWeight,
            SchedulerDistanceBeta, SchedulerGrace];
    }

    public async Task<ConfigurationResponse> GetAsync(CancellationToken cancellationToken = default) =>
        ToResponse(await repository.GetValuesAsync(Keys.All, cancellationToken));

    public async Task<ConfigurationUpdateResult> UpdateAsync(Guid updatedByUserId, UpdateConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = Validate(request, updatedByUserId);
        if (errors.Count != 0) return ConfigurationUpdateResult.Failure(errors);

        var values = ToValues(request);
        await repository.UpdateValuesAsync(values, updatedByUserId, DateTime.UtcNow, cancellationToken);
        return ConfigurationUpdateResult.Success(ToResponse(values));
    }

    private static List<string> Validate(UpdateConfigurationRequest request, Guid updatedByUserId)
    {
        var errors = new List<string>();
        if (updatedByUserId == Guid.Empty) errors.Add("The authenticated user is invalid.");
        if (!Validator.TryValidateObject(request, new ValidationContext(request), null, true)) errors.Add("One or more configuration values are out of range.");
        if (ToNullableValues(request).Any(pair => pair.Value is null)) errors.Add("All configuration values are required for a full update.");
        if (request.CriticalBatteryPercent > request.WarningBatteryPercent || request.WarningBatteryPercent > request.MinDispatchBatteryPercent
            || request.MinDispatchBatteryPercent > request.ChargeCompleteBatteryPercent)
            errors.Add("Battery thresholds must satisfy critical ≤ warning ≤ minimum dispatch ≤ charge complete.");
        if (request.NormalPriorityPoints > request.HighPriorityPoints || request.HighPriorityPoints > request.UrgentPriorityPoints)
            errors.Add("Priority points must satisfy normal ≤ high ≤ urgent.");
        return errors;
    }

    private static IReadOnlyDictionary<string, string?> ToNullableValues(UpdateConfigurationRequest request) => new Dictionary<string, string?>
    {
        [Keys.MinDispatchBattery] = ToText(request.MinDispatchBatteryPercent), [Keys.ChargeCompleteBattery] = ToText(request.ChargeCompleteBatteryPercent),
        [Keys.WarningBattery] = ToText(request.WarningBatteryPercent), [Keys.CriticalBattery] = ToText(request.CriticalBatteryPercent),
        [Keys.PickupLoading] = ToText(request.PickupLoadingSeconds), [Keys.Heartbeat] = ToText(request.HeartbeatTimeoutSeconds),
        [Keys.ScheduleMaxWait] = ToText(request.ScheduleMaxWaitMinutes), [Keys.NormalPriority] = ToText(request.NormalPriorityPoints),
        [Keys.HighPriority] = ToText(request.HighPriorityPoints), [Keys.UrgentPriority] = ToText(request.UrgentPriorityPoints),
        [Keys.QueueWaitStep] = ToText(request.QueueWaitStepMinutes), [Keys.QueueMaxWaitBonus] = ToText(request.QueueMaxWaitBonus),
        [Keys.QueueRedeliveryBonus] = ToText(request.QueueRedeliveryBonus), [Keys.SchedulerNormalWeight] = ToText(request.SchedulerNormalWeight),
        [Keys.SchedulerHighWeight] = ToText(request.SchedulerHighWeight), [Keys.SchedulerUrgentWeight] = ToText(request.SchedulerUrgentWeight),
        [Keys.SchedulerDistanceBeta] = ToText(request.SchedulerDistanceBeta), [Keys.SchedulerGrace] = ToText(request.SchedulerGraceSeconds)
    };

    private static IReadOnlyDictionary<string, string> ToValues(UpdateConfigurationRequest request) =>
        ToNullableValues(request).ToDictionary(pair => pair.Key, pair => pair.Value!, StringComparer.Ordinal);

    private static ConfigurationResponse ToResponse(IReadOnlyDictionary<string, string> values) => new()
    {
        MinDispatchBatteryPercent = GetInt(values, Keys.MinDispatchBattery), ChargeCompleteBatteryPercent = GetInt(values, Keys.ChargeCompleteBattery),
        WarningBatteryPercent = GetInt(values, Keys.WarningBattery), CriticalBatteryPercent = GetInt(values, Keys.CriticalBattery),
        PickupLoadingSeconds = GetInt(values, Keys.PickupLoading), HeartbeatTimeoutSeconds = GetInt(values, Keys.Heartbeat),
        ScheduleMaxWaitMinutes = GetInt(values, Keys.ScheduleMaxWait), NormalPriorityPoints = GetInt(values, Keys.NormalPriority),
        HighPriorityPoints = GetInt(values, Keys.HighPriority), UrgentPriorityPoints = GetInt(values, Keys.UrgentPriority),
        QueueWaitStepMinutes = GetInt(values, Keys.QueueWaitStep), QueueMaxWaitBonus = GetInt(values, Keys.QueueMaxWaitBonus),
        QueueRedeliveryBonus = GetInt(values, Keys.QueueRedeliveryBonus), SchedulerNormalWeight = GetDecimal(values, Keys.SchedulerNormalWeight),
        SchedulerHighWeight = GetDecimal(values, Keys.SchedulerHighWeight), SchedulerUrgentWeight = GetDecimal(values, Keys.SchedulerUrgentWeight),
        SchedulerDistanceBeta = GetDecimal(values, Keys.SchedulerDistanceBeta), SchedulerGraceSeconds = GetInt(values, Keys.SchedulerGrace)
    };

    private static int GetInt(IReadOnlyDictionary<string, string> values, string key) => values.TryGetValue(key, out var value)
        && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed
        : throw new InvalidOperationException($"Required configuration '{key}' is missing or invalid.");
    private static decimal GetDecimal(IReadOnlyDictionary<string, string> values, string key) => values.TryGetValue(key, out var value)
        && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed
        : throw new InvalidOperationException($"Required configuration '{key}' is missing or invalid.");
    private static string? ToText(int? value) => value?.ToString(CultureInfo.InvariantCulture);
    private static string? ToText(decimal? value) => value?.ToString(CultureInfo.InvariantCulture);
}
