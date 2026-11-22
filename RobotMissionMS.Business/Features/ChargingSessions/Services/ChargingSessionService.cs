using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.ChargingSessions.DTOs;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Business.Features.ChargingSessions.Services;

public sealed class ChargingSessionService(RobotMissionDbContext db) : IChargingSessionService
{
    public async Task<ChargingSessionListResult> ListAsync(ChargingSessionListQuery query, string? operatorFloor, CancellationToken cancellationToken = default)
    {
        if (!Validator.TryValidateObject(query, new ValidationContext(query), null, true) || (query.From is not null && query.To is not null && query.From > query.To))
            return ChargingSessionListResult.Failure(ChargingSessionReadError.InvalidQuery);

        var sessions = db.ChargingSessions.AsNoTracking().Include(x => x.Robot).Include(x => x.ChargingStation).ThenInclude(x => x.Location).ThenInclude(x => x.Zone).AsQueryable();
        if (operatorFloor is not null) sessions = sessions.Where(x => x.ChargingStation.Location.Zone.Code == operatorFloor);
        if (query.RobotId is { } robotId) sessions = sessions.Where(x => x.RobotId == robotId);
        if (!string.IsNullOrWhiteSpace(query.Station)) sessions = sessions.Where(x => x.ChargingStation.Name == query.Station.Trim() || x.ChargingStation.Location.Code == query.Station.Trim());
        if (query.From is { } from) sessions = sessions.Where(x => x.QueuedAt >= from);
        if (query.To is { } to) sessions = sessions.Where(x => x.QueuedAt < to.AddDays(1));

        var total = await sessions.CountAsync(cancellationToken);
        var rows = await sessions.OrderByDescending(x => x.QueuedAt).ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return ChargingSessionListResult.Success(new PagedResponse<ChargingSessionResponse>
        {
            Items = rows.Select(x => ToResponse(x, DateTime.UtcNow)).ToList(), Page = query.Page, PageSize = query.PageSize, Total = total
        });
    }

    public async Task<ChargingSessionCommandResult> QueueAsync(Guid robotId, Guid stationId, Guid? actorUserId, DateTime now, CancellationToken cancellationToken = default)
    {
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var robot = await db.Robots.SingleOrDefaultAsync(x => x.Id == robotId, cancellationToken);
        if (robot is null) return ChargingSessionCommandResult.Failure(ChargingSessionCommandError.RobotNotFound);
        var station = await db.ChargingStations.Include(x => x.Location).ThenInclude(x => x.Zone).SingleOrDefaultAsync(x => x.Id == stationId, cancellationToken);
        if (station is null) return ChargingSessionCommandResult.Failure(ChargingSessionCommandError.StationNotFound);
        if (station.Location.ZoneId != robot.ZoneId) return ChargingSessionCommandResult.Failure(ChargingSessionCommandError.FloorMismatch);
        if (await db.ChargingSessions.AnyAsync(x => x.RobotId == robotId && (x.Status == ChargingSessionStatus.Queued || x.Status == ChargingSessionStatus.Charging), cancellationToken))
            return ChargingSessionCommandResult.Failure(ChargingSessionCommandError.ActiveSessionExists);
        if (station.Status != StationStatus.Operational) return ChargingSessionCommandResult.Failure(ChargingSessionCommandError.StationUnavailable);
        if (robot.State != RobotState.Available) return ChargingSessionCommandResult.Failure(ChargingSessionCommandError.RobotNotAvailable);

        var session = new ChargingSession { Id = Guid.NewGuid(), RobotId = robotId, ChargingStationId = stationId, QueuedAt = now, Status = ChargingSessionStatus.Queued };
        robot.State = RobotState.OnTrip;
        db.ChargingSessions.Add(session);
        db.ActivityLogs.Add(new ActivityLog { UserId = actorUserId, Timestamp = now, Action = "CHARGING_SESSION_QUEUED", TargetObject = $"ChargingSession:{session.Id:D}" });
        await db.SaveChangesAsync(cancellationToken);
        if (ownsTransaction) await transaction!.CommitAsync(cancellationToken);
        session.Robot = robot; session.ChargingStation = station;
        return ChargingSessionCommandResult.Success(ToResponse(session, now));
    }

    public async Task<ChargingSessionCommandResult> QueueToHomeStationAsync(Guid robotId, Guid? actorUserId, DateTime now, CancellationToken cancellationToken = default)
    {
        var stationId = await db.Robots.AsNoTracking().Where(x => x.Id == robotId)
            .Select(x => (Guid?)x.HomeChargingStationId).SingleOrDefaultAsync(cancellationToken);
        if (stationId is null)
        {
            var robotExists = await db.Robots.AsNoTracking().AnyAsync(x => x.Id == robotId, cancellationToken);
            return ChargingSessionCommandResult.Failure(robotExists ? ChargingSessionCommandError.RobotHasNoHomeStation : ChargingSessionCommandError.RobotNotFound);
        }
        return await QueueAsync(robotId, stationId.Value, actorUserId, now, cancellationToken);
    }

    public async Task<ChargingSessionCommandResult> StartAsync(Guid sessionId, DateTime now, CancellationToken cancellationToken = default)
    {
        var session = await db.ChargingSessions.Include(x => x.Robot).Include(x => x.ChargingStation).ThenInclude(x => x.Location).ThenInclude(x => x.Zone).SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null) return ChargingSessionCommandResult.Failure(ChargingSessionCommandError.SessionNotFound);
        if (session.Status != ChargingSessionStatus.Queued) return ChargingSessionCommandResult.Failure(ChargingSessionCommandError.SessionNotQueued);
        if (session.ChargingStation.Status != StationStatus.Operational) return ChargingSessionCommandResult.Failure(ChargingSessionCommandError.StationUnavailable);
        var active = await db.ChargingSessions.CountAsync(x => x.ChargingStationId == session.ChargingStationId && x.Status == ChargingSessionStatus.Charging, cancellationToken);
        if (active >= session.ChargingStation.Capacity) return ChargingSessionCommandResult.Failure(ChargingSessionCommandError.StationAtCapacity);

        session.Status = ChargingSessionStatus.Charging;
        session.StartedAt = now;
        session.BatteryAtStart = session.Robot.Battery;
        session.Robot.State = RobotState.Charging;
        db.ActivityLogs.Add(new ActivityLog { Timestamp = now, Action = "CHARGING_SESSION_STARTED", TargetObject = $"ChargingSession:{session.Id:D}" });
        await db.SaveChangesAsync(cancellationToken);
        return ChargingSessionCommandResult.Success(ToResponse(session, now));
    }

    public Task<ChargingSessionCommandResult> CompleteAsync(Guid sessionId, int batteryAtEnd, DateTime now, CancellationToken cancellationToken = default) => FinishAsync(sessionId, batteryAtEnd, ChargingSessionStatus.Completed, now, cancellationToken);
    public Task<ChargingSessionCommandResult> InterruptAsync(Guid sessionId, int batteryAtEnd, DateTime now, CancellationToken cancellationToken = default) => FinishAsync(sessionId, batteryAtEnd, ChargingSessionStatus.Interrupted, now, cancellationToken);

    private async Task<ChargingSessionCommandResult> FinishAsync(Guid sessionId, int batteryAtEnd, ChargingSessionStatus finalStatus, DateTime now, CancellationToken cancellationToken)
    {
        if (batteryAtEnd is < 0 or > 100) return ChargingSessionCommandResult.Failure(ChargingSessionCommandError.InvalidBattery);
        var session = await db.ChargingSessions.Include(x => x.Robot).Include(x => x.ChargingStation).ThenInclude(x => x.Location).ThenInclude(x => x.Zone).SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null) return ChargingSessionCommandResult.Failure(ChargingSessionCommandError.SessionNotFound);
        if (session.Status != ChargingSessionStatus.Charging) return ChargingSessionCommandResult.Failure(ChargingSessionCommandError.SessionNotCharging);

        session.Status = finalStatus;
        session.EndedAt = now;
        session.BatteryAtEnd = batteryAtEnd;
        session.Robot.Battery = batteryAtEnd;
        session.Robot.State = RobotState.Available;
        db.ActivityLogs.Add(new ActivityLog { Timestamp = now, Action = finalStatus == ChargingSessionStatus.Completed ? "CHARGING_SESSION_COMPLETED" : "CHARGING_SESSION_INTERRUPTED", TargetObject = $"ChargingSession:{session.Id:D}" });
        await db.SaveChangesAsync(cancellationToken);
        return ChargingSessionCommandResult.Success(ToResponse(session, now));
    }

    private static ChargingSessionResponse ToResponse(ChargingSession session, DateTime now)
    {
        var waitEnd = session.StartedAt ?? session.EndedAt ?? now;
        return new ChargingSessionResponse
        {
            Id = session.Id, RobotId = session.RobotId, RobotCode = session.Robot.RobotCode,
            ChargingStationId = session.ChargingStationId, Station = session.ChargingStation.Name,
            Floor = session.ChargingStation.Location.Zone.Code, QueuedAt = session.QueuedAt,
            StartedAt = session.StartedAt, EndedAt = session.EndedAt, BatteryAtStart = session.BatteryAtStart,
            BatteryAtEnd = session.BatteryAtEnd, Status = session.Status.ToString().ToUpperInvariant(),
            WaitingDurationSeconds = Math.Max(0, (long)(waitEnd - session.QueuedAt).TotalSeconds)
        };
    }
}
