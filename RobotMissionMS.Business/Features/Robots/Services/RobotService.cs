using System.ComponentModel.DataAnnotations;
using RobotMissionMS.Business.Features.Robots.DTOs;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Repositories.Interfaces;

namespace RobotMissionMS.Business.Features.Robots.Services;

public sealed class RobotService(IRobotRepository repository) : IRobotService
{
    public async Task<RobotListResult> GetRobotsAsync(
        RobotListQuery    query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!Validator.TryValidateObject(query, new ValidationContext(query), null, validateAllProperties: true))
            return RobotListResult.Failure(RobotListError.InvalidQuery);

        RobotState? stateFilter = null;
        if (!string.IsNullOrWhiteSpace(query.State))
        {
            if (!TryParseRobotState(query.State, out var parsed))
                return RobotListResult.Failure(RobotListError.InvalidQuery);
            stateFilter = parsed;
        }

        var rows = await repository.GetRobotsAsync(
            string.IsNullOrWhiteSpace(query.Floor) ? null : query.Floor,
            string.IsNullOrWhiteSpace(query.Type)  ? null : query.Type,
            stateFilter,
            query.IsConnected,
            cancellationToken);

        var response = rows
            .Select(r => new RobotListItemResponse
            {
                Id              = r.Id,
                RobotCode       = r.RobotCode,
                Type            = r.TypeCode,
                TypeName        = r.TypeName,
                Floor           = r.FloorCode,
                FloorName       = r.FloorName,
                State           = RobotStateToString(r.State),
                IsConnected     = r.IsConnected,
                Battery         = r.Battery,
                CurrentX        = r.CurrentX,
                CurrentY        = r.CurrentY,
                ActiveTripCode  = r.ActiveTripCode,
                LastHeartbeatAt = r.LastHeartbeatAt,
            })
            .ToList();

        return RobotListResult.Success(response);
    }

    public async Task<RobotDetailResult> GetRobotDetailAsync(
        Guid              robotId,
        CancellationToken cancellationToken = default)
    {
        var row = await repository.GetRobotByIdAsync(robotId, cancellationToken);
        if (row is null)
            return RobotDetailResult.Failure(RobotDetailError.NotFound);

        // Build slot list: enumerate 1..Slots, overlay occupied missions by SlotNo
        var slotMap = row.MissionSlots.ToDictionary(m => m.SlotNo);
        var slots = Enumerable.Range(1, row.Slots)
            .Select(slotNo => slotMap.TryGetValue(slotNo, out var m)
                ? new RobotSlotInfo
                {
                    SlotNo         = slotNo,
                    IsOccupied     = true,
                    MissionCode    = m.MissionCode,
                    ItemCode       = m.ItemCode,
                    ItemName       = m.ItemName,
                    Quantity       = m.Quantity,
                    ToLocationCode = m.ToLocationCode,
                    ToLocationName = m.ToLocationName,
                    MissionStatus  = m.MissionStatus,
                }
                : new RobotSlotInfo { SlotNo = slotNo, IsOccupied = false })
            .ToList();

        RobotActiveTripInfo? activeTrip = null;
        if (row.ActiveTrip is { } t)
        {
            activeTrip = new RobotActiveTripInfo
            {
                TripId    = t.TripId,
                TripCode  = t.TripCode,
                Status    = TripStatusToString(t.Status),
                StartedAt = t.StartedAt,
                Stops     = t.Stops
                    .Select(s => new RobotTripStopInfo
                    {
                        Seq          = s.Seq,
                        LocationCode = s.LocationCode,
                        LocationName = s.LocationName,
                        ArrivedAt    = s.ArrivedAt,
                        DepartedAt   = s.DepartedAt,
                    })
                    .ToList(),
            };
        }

        var openAlerts = row.OpenAlerts
            .Select(a => new RobotAlertInfo
            {
                AlertId   = a.AlertId,
                AlertType = a.AlertType,
                Severity  = AlertSeverityToString(a.Severity),
                Category  = AlertCategoryToString(a.Category),
                Message   = a.Message,
                CreatedAt = a.CreatedAt,
            })
            .ToList();

        var detail = new RobotDetailResponse
        {
            Id              = row.Id,
            RobotCode       = row.RobotCode,
            Type            = row.TypeCode,
            TypeName        = row.TypeName,
            Floor           = row.FloorCode,
            FloorName       = row.FloorName,
            State           = RobotStateToString(row.State),
            IsConnected     = row.IsConnected,
            Battery         = row.Battery,
            CurrentX        = row.CurrentX,
            CurrentY        = row.CurrentY,
            LastHeartbeatAt = row.LastHeartbeatAt,
            HomeStation     = row.HomeStationCode,
            Slots           = slots,
            ActiveTrip      = activeTrip,
            OpenAlerts      = openAlerts,
        };

        return RobotDetailResult.Success(detail);
    }

    public async Task<RobotHistoryResult> GetRobotHistoryAsync(
        Guid              robotId,
        RobotHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var now    = DateTime.UtcNow;
        var toUtc  = (query.To   ?? DateTimeOffset.UtcNow).UtcDateTime;
        var fromUtc = (query.From ?? DateTimeOffset.UtcNow.AddDays(-7)).UtcDateTime;

        if (fromUtc > toUtc)
            (fromUtc, toUtc) = (toUtc, fromUtc);

        var data = await repository.GetRobotHistoryAsync(robotId, fromUtc, toUtc, cancellationToken);
        if (data is null)
            return RobotHistoryResult.Failure(RobotHistoryError.NotFound);

        var trips = data.Trips.Select(t => new RobotHistoryTripItem
        {
            TripId       = t.TripId,
            TripCode     = t.TripCode,
            Status       = TripStatusToString(t.Status),
            MissionCount = t.MissionCount,
            StartedAt    = t.StartedAt,
            CompletedAt  = t.CompletedAt,
            CreatedAt    = t.CreatedAt,
        }).ToList();

        var alerts = data.Alerts.Select(a => new RobotHistoryAlertItem
        {
            AlertId   = a.AlertId,
            AlertType = a.AlertType,
            Severity  = AlertSeverityToString(a.Severity),
            Category  = AlertCategoryToString(a.Category),
            Status    = AlertStatusToString(a.Status),
            Message   = a.Message,
            CreatedAt = a.CreatedAt,
            ResolvedAt = a.ResolvedAt,
        }).ToList();

        var sessions = data.ChargingSessions.Select(cs => new RobotHistoryChargingSessionItem
        {
            SessionId           = cs.SessionId,
            StationName         = cs.StationName,
            StationLocationCode = cs.StationLocationCode,
            StartedAt           = cs.StartedAt,
            EndedAt             = cs.EndedAt,
            DurationMinutes     = cs.EndedAt.HasValue
                ? (cs.EndedAt.Value - cs.StartedAt).TotalMinutes
                : null,
        }).ToList();

        return RobotHistoryResult.Success(new RobotHistoryResponse
        {
            Trips            = trips,
            Alerts           = alerts,
            ChargingSessions = sessions,
        });
    }

    private static string AlertStatusToString(AlertStatus status) => status switch
    {
        AlertStatus.Open         => "OPEN",
        AlertStatus.Acknowledged => "ACKNOWLEDGED",
        AlertStatus.Resolved     => "RESOLVED",
        _                        => status.ToString().ToUpperInvariant()
    };

    public async Task<CreateRobotResult> CreateRobotAsync(
        CreateRobotRequest request,
        CancellationToken  cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Validator.TryValidateObject(request, new ValidationContext(request), null, validateAllProperties: true))
            return CreateRobotResult.Failure(CreateRobotError.InvalidRequest);

        var typeId = await repository.FindRobotTypeIdByCodeAsync(request.RobotTypeCode.Trim(), cancellationToken);
        if (typeId is null)
            return CreateRobotResult.Failure(CreateRobotError.RobotTypeNotFound);

        var zoneId = await repository.FindZoneIdByCodeAsync(request.ZoneCode.Trim(), cancellationToken);
        if (zoneId is null)
            return CreateRobotResult.Failure(CreateRobotError.ZoneNotFound);

        if (request.HomeChargingStationId.HasValue)
        {
            var stationExists = await repository.ChargingStationExistsAsync(
                request.HomeChargingStationId.Value, cancellationToken);
            if (!stationExists)
                return CreateRobotResult.Failure(CreateRobotError.HomeStationNotFound);
        }

        var repoResult = await repository.CreateRobotAsync(
            request.RobotCode.Trim(),
            typeId.Value,
            zoneId.Value,
            request.HomeChargingStationId,
            cancellationToken);

        if (!repoResult.IsSuccess)
            return CreateRobotResult.Failure(CreateRobotError.DuplicateCode);

        var row = repoResult.Row!;
        return CreateRobotResult.Success(new CreateRobotResponse
        {
            Id              = row.Id,
            RobotCode       = row.RobotCode,
            Type            = row.TypeCode,
            TypeName        = row.TypeName,
            Floor           = row.FloorCode,
            FloorName       = row.FloorName,
            State           = row.State,
            IsConnected     = row.IsConnected,
            Battery         = row.Battery,
            HomeStationCode = row.HomeStationCode,
            CreatedAt       = row.CreatedAt,
        });
    }

    public async Task<UpdateRobotResult> UpdateRobotAsync(
        Guid               robotId,
        UpdateRobotRequest request,
        CancellationToken  cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.HomeChargingStationId.HasValue)
        {
            var stationExists = await repository.ChargingStationExistsAsync(
                request.HomeChargingStationId.Value, cancellationToken);
            if (!stationExists)
                return UpdateRobotResult.Failure(UpdateRobotError.HomeStationNotFound);
        }

        var row = await repository.UpdateRobotAsync(robotId, request.HomeChargingStationId, cancellationToken);
        if (row is null)
            return UpdateRobotResult.Failure(UpdateRobotError.NotFound);

        return UpdateRobotResult.Success(new CreateRobotResponse
        {
            Id              = row.Id,
            RobotCode       = row.RobotCode,
            Type            = row.TypeCode,
            TypeName        = row.TypeName,
            Floor           = row.FloorCode,
            FloorName       = row.FloorName,
            State           = row.State,
            IsConnected     = row.IsConnected,
            Battery         = row.Battery,
            HomeStationCode = row.HomeStationCode,
            CreatedAt       = row.CreatedAt,
        });
    }

    public async Task<DeleteRobotResult> DeleteRobotAsync(
        Guid              robotId,
        CancellationToken cancellationToken = default)
    {
        var repoResult = await repository.DeleteRobotAsync(robotId, cancellationToken);
        if (repoResult.IsSuccess)
            return DeleteRobotResult.Success();

        return repoResult.Error switch
        {
            DeleteRobotRepositoryError.NotFound     => DeleteRobotResult.Failure(DeleteRobotError.NotFound),
            DeleteRobotRepositoryError.OnActiveTrip => DeleteRobotResult.Failure(DeleteRobotError.RobotOnActiveTrip),
            _ => throw new InvalidOperationException("Unexpected delete robot repository error.")
        };
    }

    public async Task<SuspendRobotResult> SuspendRobotAsync(
        Guid              robotId,
        CancellationToken cancellationToken = default)
    {
        var repoResult = await repository.SuspendRobotAsync(robotId, cancellationToken);
        if (repoResult.IsSuccess) return SuspendRobotResult.Success();
        return repoResult.Error switch
        {
            SuspendRobotRepositoryError.NotFound         => SuspendRobotResult.Failure(SuspendRobotError.NotFound),
            SuspendRobotRepositoryError.AlreadySuspended => SuspendRobotResult.Failure(SuspendRobotError.AlreadySuspended),
            _ => throw new InvalidOperationException("Unexpected suspend robot repository error.")
        };
    }

    public async Task<ResumeRobotResult> ResumeRobotAsync(
        Guid              robotId,
        CancellationToken cancellationToken = default)
    {
        var repoResult = await repository.ResumeRobotAsync(robotId, cancellationToken);
        if (repoResult.IsSuccess) return ResumeRobotResult.Success();
        return repoResult.Error switch
        {
            ResumeRobotRepositoryError.NotFound     => ResumeRobotResult.Failure(ResumeRobotError.NotFound),
            ResumeRobotRepositoryError.NotSuspended => ResumeRobotResult.Failure(ResumeRobotError.NotSuspended),
            _ => throw new InvalidOperationException("Unexpected resume robot repository error.")
        };
    }

    public async Task<ChargeRobotResult> ChargeRobotAsync(
        Guid              robotId,
        CancellationToken cancellationToken = default)
    {
        var repoResult = await repository.ChargeRobotAsync(robotId, cancellationToken);
        if (repoResult.IsSuccess) return ChargeRobotResult.Success();
        return repoResult.Error switch
        {
            ChargeRobotRepositoryError.NotFound     => ChargeRobotResult.Failure(ChargeRobotError.NotFound),
            ChargeRobotRepositoryError.NotAvailable => ChargeRobotResult.Failure(ChargeRobotError.NotAvailable),
            _ => throw new InvalidOperationException("Unexpected charge robot repository error.")
        };
    }

    public async Task<MaintenanceRobotResult> MaintenanceRobotAsync(
        Guid              robotId,
        CancellationToken cancellationToken = default)
    {
        var repoResult = await repository.MaintenanceRobotAsync(robotId, cancellationToken);
        if (repoResult.IsSuccess) return MaintenanceRobotResult.Success();
        return repoResult.Error switch
        {
            MaintenanceRobotRepositoryError.NotFound            => MaintenanceRobotResult.Failure(MaintenanceRobotError.NotFound),
            MaintenanceRobotRepositoryError.OnActiveTrip        => MaintenanceRobotResult.Failure(MaintenanceRobotError.OnActiveTrip),
            MaintenanceRobotRepositoryError.AlreadyInMaintenance => MaintenanceRobotResult.Failure(MaintenanceRobotError.AlreadyInMaintenance),
            _ => throw new InvalidOperationException("Unexpected maintenance robot repository error.")
        };
    }

    public async Task<BackToServiceRobotResult> BackToServiceAsync(
        Guid              robotId,
        CancellationToken cancellationToken = default)
    {
        var repoResult = await repository.BackToServiceAsync(robotId, cancellationToken);
        if (repoResult.IsSuccess) return BackToServiceRobotResult.Success();
        return repoResult.Error switch
        {
            BackToServiceRepositoryError.NotFound         => BackToServiceRobotResult.Failure(BackToServiceRobotError.NotFound),
            BackToServiceRepositoryError.NotInMaintenance => BackToServiceRobotResult.Failure(BackToServiceRobotError.NotInMaintenance),
            _ => throw new InvalidOperationException("Unexpected back-to-service repository error.")
        };
    }

    private static bool TryParseRobotState(string value, out RobotState result)
    {
        result = value.ToUpperInvariant() switch
        {
            "INACTIVE"    => RobotState.Inactive,
            "AVAILABLE"   => RobotState.Available,
            "ON_TRIP"     => RobotState.OnTrip,
            "CHARGING"    => RobotState.Charging,
            "ERROR"       => RobotState.Error,
            "MAINTENANCE" => RobotState.Maintenance,
            _             => (RobotState)(-1)
        };
        return (int)result >= 0;
    }

    private static string RobotStateToString(RobotState state) => state switch
    {
        RobotState.Inactive    => "INACTIVE",
        RobotState.Available   => "AVAILABLE",
        RobotState.OnTrip      => "ON_TRIP",
        RobotState.Charging    => "CHARGING",
        RobotState.Error       => "ERROR",
        RobotState.Maintenance => "MAINTENANCE",
        _                      => state.ToString().ToUpperInvariant()
    };

    private static string TripStatusToString(TripStatus status) => status switch
    {
        TripStatus.Planned    => "PLANNED",
        TripStatus.Dispatched => "DISPATCHED",
        TripStatus.InProgress => "IN_PROGRESS",
        TripStatus.Completed  => "COMPLETED",
        TripStatus.Aborted    => "ABORTED",
        _                     => status.ToString().ToUpperInvariant()
    };

    private static string AlertSeverityToString(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Info     => "INFO",
        AlertSeverity.Warning  => "WARNING",
        AlertSeverity.Critical => "CRITICAL",
        _                      => severity.ToString().ToUpperInvariant()
    };

    private static string AlertCategoryToString(AlertCategory category) => category switch
    {
        AlertCategory.Robot   => "ROBOT",
        AlertCategory.Mission => "MISSION",
        AlertCategory.System  => "SYSTEM",
        _                     => category.ToString().ToUpperInvariant()
    };
}
