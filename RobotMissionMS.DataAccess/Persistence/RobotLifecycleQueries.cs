using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.DataAccess.Persistence;

public static class RobotLifecycleQueries
{
    public static Task<Robot?> LockRobotAsync(this RobotMissionDbContext db, Guid robotId, CancellationToken ct) =>
        db.Robots.FromSqlInterpolated($"SELECT * FROM public.\"Robots\" WHERE \"Id\" = {robotId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);

    public static async Task<bool> HasRobotWorkAsync(this RobotMissionDbContext db, Guid robotId, CancellationToken ct,
        Guid? finishingTripId = null, Guid? finishingChargingSessionId = null)
    {
        if (await db.Trips.AnyAsync(trip => trip.RobotId == robotId && trip.Id != finishingTripId &&
                (trip.Status == TripStatus.Planned || trip.Status == TripStatus.Dispatched || trip.Status == TripStatus.InProgress), ct))
            return true;

        return await db.ChargingSessions.AnyAsync(session => session.RobotId == robotId && session.Id != finishingChargingSessionId &&
            (session.Status == ChargingSessionStatus.Queued || session.Status == ChargingSessionStatus.Charging), ct);
    }

    public static bool IsAvailableForWork(this Robot robot) =>
        robot.State == RobotState.Available && !robot.AwaitingFirstConnection
        && robot.SuspendRequestedAt is null && robot.DecommissionedAt is null;

    public static async Task SetRobotStateAfterWorkAsync(this RobotMissionDbContext db, Robot robot, CancellationToken ct,
        Guid? finishingTripId = null, Guid? finishingChargingSessionId = null)
    {
        if (robot.DecommissionedAt is not null || robot.AwaitingFirstConnection)
        {
            robot.State = RobotState.Inactive;
            return;
        }

        if (await db.HasRobotWorkAsync(robot.Id, ct, finishingTripId, finishingChargingSessionId))
            return;

        if (robot.SuspendRequestedAt is not null)
            robot.State = RobotState.Inactive;
        else if (robot.State is RobotState.OnTrip or RobotState.Charging)
            robot.State = RobotState.Available;
    }
}
