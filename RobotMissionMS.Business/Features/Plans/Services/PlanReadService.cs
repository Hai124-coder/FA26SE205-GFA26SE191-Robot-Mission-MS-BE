using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.Business.Common.DTOs;
using RobotMissionMS.Business.Features.Plans.DTOs;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Business.Features.Plans.Services;

public sealed class PlanReadService(RobotMissionDbContext db) : IPlanReadService
{
    public async Task<PlanReadResult<PlanDetailResponse?>> GetCurrentAsync(PlanCurrentQuery query, string? operatorFloor, CancellationToken cancellationToken = default)
    {
        if (!Valid(query)) return PlanReadResult<PlanDetailResponse?>.Failure(PlanReadError.InvalidQuery);
        var floor = operatorFloor ?? Text(query.Floor);
        if (floor is null) return PlanReadResult<PlanDetailResponse?>.Failure(PlanReadError.InvalidQuery);
        var plans = LoadPlans().Where(value => value.Status == DispatchPlanStatus.Draft);
        plans = plans.Where(value => value.Zone.Code == floor);
        var plan = await plans.SingleOrDefaultAsync(cancellationToken);
        return PlanReadResult<PlanDetailResponse?>.Success(plan is null ? null : ToDetail(plan));
    }

    public async Task<PlanReadResult<PagedResponse<PlanSummaryResponse>>> GetPlansAsync(PlanListQuery query, string? operatorFloor, CancellationToken cancellationToken = default)
    {
        if (!Valid(query) || !TryPlanStatuses(query.Status, out var statuses)) return PlanReadResult<PagedResponse<PlanSummaryResponse>>.Failure(PlanReadError.InvalidQuery);
        var floor = operatorFloor ?? Text(query.Floor);
        var plans = db.DispatchPlans.AsNoTracking().Include(value => value.Zone).Include(value => value.Trips).Include(value => value.Tasks).AsQueryable();
        if (floor is not null) plans = plans.Where(value => value.Zone.Code == floor);
        if (statuses is not null) plans = plans.Where(value => statuses.Contains(value.Status));
        if (query.From is { } from) plans = plans.Where(value => value.CreatedAt >= from);
        if (query.To is { } to) plans = plans.Where(value => value.CreatedAt < to.AddDays(1));
        var total = await plans.CountAsync(cancellationToken);
        var items = await plans.OrderByDescending(value => value.CreatedAt).ThenBy(value => value.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return PlanReadResult<PagedResponse<PlanSummaryResponse>>.Success(new PagedResponse<PlanSummaryResponse> { Items = items.Select(ToSummary).ToList(), Page = query.Page, PageSize = query.PageSize, Total = total });
    }

    public async Task<PlanReadResult<PlanDetailResponse>> GetPlanAsync(Guid id, string? operatorFloor, CancellationToken cancellationToken = default)
    {
        var plan = await LoadPlans().SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (plan is null) return PlanReadResult<PlanDetailResponse>.Failure(PlanReadError.NotFound);
        if (operatorFloor is not null && plan.Zone.Code != operatorFloor) return PlanReadResult<PlanDetailResponse>.Failure(PlanReadError.FloorForbidden);
        return PlanReadResult<PlanDetailResponse>.Success(ToDetail(plan));
    }

    public async Task<PlanReadResult<PagedResponse<TripListItemResponse>>> GetTripsAsync(TripListQuery query, string? operatorFloor, CancellationToken cancellationToken = default)
    {
        if (!Valid(query) || !TryTripStatuses(query.Status, out var statuses)) return PlanReadResult<PagedResponse<TripListItemResponse>>.Failure(PlanReadError.InvalidQuery);
        var floor = operatorFloor ?? Text(query.Floor);
        var trips = db.Trips.AsNoTracking().Include(value => value.Zone).Include(value => value.Robot).Include(value => value.Missions).AsQueryable();
        if (floor is not null) trips = trips.Where(value => value.Zone.Code == floor);
        if (statuses is not null) trips = trips.Where(value => statuses.Contains(value.Status));
        if (query.RobotId is { } robotId) trips = trips.Where(value => value.RobotId == robotId);
        if (query.From is { } from) trips = trips.Where(value => value.CreatedAt >= from);
        if (query.To is { } to) trips = trips.Where(value => value.CreatedAt < to.AddDays(1));
        var total = await trips.CountAsync(cancellationToken);
        var items = await trips.OrderByDescending(value => value.CreatedAt).ThenBy(value => value.TripCode).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        var tripIds = items.Select(value => value.Id).ToList();
        var dispatches = await db.RobotCommandOutbox.AsNoTracking()
            .Where(value => tripIds.Contains(value.TripId) && value.CommandType == "DISPATCH_TRIP")
            .ToDictionaryAsync(value => value.TripId, cancellationToken);
        return PlanReadResult<PagedResponse<TripListItemResponse>>.Success(new PagedResponse<TripListItemResponse> { Items = items.Select(value => ToTripItem(value, dispatches.GetValueOrDefault(value.Id))).ToList(), Page = query.Page, PageSize = query.PageSize, Total = total });
    }

    public async Task<PlanReadResult<TripDetailResponse>> GetTripAsync(Guid id, string? operatorFloor, CancellationToken cancellationToken = default)
    {
        var trip = await db.Trips.AsNoTracking().AsSplitQuery().Include(value => value.Zone).Include(value => value.Robot).ThenInclude(value => value.RobotType)
            .Include(value => value.TripStops).ThenInclude(value => value.Location).Include(value => value.Missions).ThenInclude(value => value.MissionType).Include(value => value.Missions).ThenInclude(value => value.ToLocation)
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (trip is null) return PlanReadResult<TripDetailResponse>.Failure(PlanReadError.NotFound);
        if (operatorFloor is not null && trip.Zone.Code != operatorFloor) return PlanReadResult<TripDetailResponse>.Failure(PlanReadError.FloorForbidden);
        var dispatch = await db.RobotCommandOutbox.AsNoTracking().SingleOrDefaultAsync(value => value.TripId == trip.Id && value.CommandType == "DISPATCH_TRIP", cancellationToken);
        return PlanReadResult<TripDetailResponse>.Success(new TripDetailResponse
        {
            Id = trip.Id, TripCode = trip.TripCode, Status = Code(trip.Status), Floor = trip.Zone.Code, RobotId = trip.RobotId, RobotCode = trip.Robot.RobotCode,
            CreatedAt = Offset(trip.CreatedAt), DispatchedAt = Offset(trip.DispatchedAt), StartedAt = Offset(trip.StartedAt), CompletedAt = Offset(trip.CompletedAt), TaskCount = trip.Missions.Count,
            Dispatch = ToDispatchStatus(dispatch),
            CurrentStop = ToCurrentStop(trip.TripStops),
            Stops = trip.TripStops.OrderBy(value => value.Seq).Select(value => new TripStopResponse { Id = value.Id, Sequence = value.Seq, Location = value.Location.Code, Action = Code(value.Action), Status = Code(value.Status), ArrivedAt = Offset(value.ArrivedAt), DepartedAt = Offset(value.DepartedAt) }).ToList(),
            Tasks = trip.Missions.OrderBy(value => value.SlotNo).ThenBy(value => value.MissionCode).Select(value => new TripTaskResponse { MissionId = value.Id, MissionCode = value.MissionCode, Status = Code(value.Status), MissionType = value.MissionType.Code, SlotNo = value.SlotNo, CompartmentStatus = CompartmentStatus(value.Status), Department = value.ToLocation?.Code, PickedUpAt = Offset(value.PickedUpAt), CompletedAt = Offset(value.CompletedAt) }).ToList()
        });
    }

    private IQueryable<DispatchPlan> LoadPlans() => db.DispatchPlans.AsNoTracking().AsSplitQuery().Include(value => value.Zone)
        .Include(value => value.Trips).ThenInclude(value => value.Robot).ThenInclude(value => value.RobotType)
        .Include(value => value.Trips).ThenInclude(value => value.PickupLocation)
        .Include(value => value.Trips).ThenInclude(value => value.Stops).ThenInclude(value => value.Location)
        .Include(value => value.Tasks).ThenInclude(value => value.Mission)
        .Include(value => value.Tasks).ThenInclude(value => value.PlanTrip).ThenInclude(value => value!.Robot)
        .Include(value => value.Tasks).ThenInclude(value => value.DeliveryPlanStop);

    private static PlanDetailResponse ToDetail(DispatchPlan plan) => new()
    {
        Id = plan.Id, Floor = plan.Zone.Code, Status = Code(plan.Status), Source = Code(plan.Source), Reason = plan.Reason, CreatedAt = Offset(plan.CreatedAt), ReleasedAt = Offset(plan.ReleasedAt), ManualEditCount = plan.ManualEditCount, Version = plan.Version,
        TripCount = plan.Trips.Count, AssignedTaskCount = plan.Tasks.Count(value => value.Status == DispatchPlanTaskStatus.Assigned), WaitingTaskCount = plan.Tasks.Count(value => value.Status == DispatchPlanTaskStatus.Waiting), EstimatedLateTaskCount = plan.Tasks.Count(value => value.LateReason is not null),
        EstimatedDistanceM = plan.EstimatedDistanceM, BaselineDistanceM = plan.BaselineDistanceM, EstimatedDurationSec = plan.EstimatedDurationSec, BaselineDurationSec = plan.BaselineDurationSec,
        Trips = plan.Trips.OrderBy(value => value.Seq).Select(value => new PlanTripResponse { Id = value.Id, Sequence = value.Seq, RobotId = value.RobotId, RobotCode = value.Robot.RobotCode, RobotType = value.Robot.RobotType.TypeCode, PickupLocation = value.PickupLocation?.Code, EstimatedStartAt = Offset(value.EstimatedStartAt), EstimatedEndAt = Offset(value.EstimatedEndAt), EstimatedDistanceM = value.EstimatedDistanceM,
            Stops = value.Stops.OrderBy(stop => stop.Seq).Select(stop => new PlanStopResponse { Id = stop.Id, Sequence = stop.Seq, Location = stop.Location.Code, Action = Code(stop.Action), EstimatedArrivedAt = Offset(stop.EstimatedArrivedAt), EstimatedDepartedAt = Offset(stop.EstimatedDepartedAt), CumulativeDistanceM = stop.CumulativeDistanceM }).ToList() }).ToList(),
        Tasks = plan.Tasks.OrderBy(value => value.Status).ThenByDescending(value => value.Priority).ThenBy(value => value.Mission.MissionCode).Select(value => new PlanTaskResponse { Id = value.Id, MissionId = value.MissionId, MissionCode = value.Mission.MissionCode, Status = Code(value.Status), Priority = Code(value.Priority), RobotCode = value.PlanTrip?.Robot.RobotCode, SlotNo = value.SlotNo, DeliveryStopSequence = value.DeliveryPlanStop?.Seq, EstimatedDeliveryAt = Offset(value.EstimatedDeliveryAt), WaitReason = value.WaitReason, LateReason = value.LateReason }).ToList()
    };

    private static PlanSummaryResponse ToSummary(DispatchPlan plan) => new()
    {
        Id = plan.Id, Floor = plan.Zone.Code, Status = Code(plan.Status), Source = Code(plan.Source), Reason = plan.Reason, CreatedAt = Offset(plan.CreatedAt), ReleasedAt = Offset(plan.ReleasedAt), ManualEditCount = plan.ManualEditCount, Version = plan.Version,
        TripCount = plan.Trips.Count, AssignedTaskCount = plan.Tasks.Count(value => value.Status == DispatchPlanTaskStatus.Assigned), WaitingTaskCount = plan.Tasks.Count(value => value.Status == DispatchPlanTaskStatus.Waiting), EstimatedLateTaskCount = plan.Tasks.Count(value => value.LateReason is not null),
        EstimatedDistanceM = plan.EstimatedDistanceM, BaselineDistanceM = plan.BaselineDistanceM, EstimatedDurationSec = plan.EstimatedDurationSec, BaselineDurationSec = plan.BaselineDurationSec
    };

    private static TripListItemResponse ToTripItem(Trip trip, RobotCommandOutbox? dispatch) => new() { Id = trip.Id, TripCode = trip.TripCode, Status = Code(trip.Status), Floor = trip.Zone.Code, RobotId = trip.RobotId, RobotCode = trip.Robot.RobotCode, CreatedAt = Offset(trip.CreatedAt), DispatchedAt = Offset(trip.DispatchedAt), StartedAt = Offset(trip.StartedAt), CompletedAt = Offset(trip.CompletedAt), TaskCount = trip.Missions.Count, Dispatch = ToDispatchStatus(dispatch) };
    private static TripDispatchStatusResponse? ToDispatchStatus(RobotCommandOutbox? dispatch) => dispatch is null ? null : new() { Status = Code(dispatch.Status), AttemptCount = dispatch.AttemptCount, NextAttemptAt = Offset(dispatch.NextAttemptAt), SentAt = Offset(dispatch.SentAt), UpdatedAt = Offset(dispatch.UpdatedAt) };
    private static TripCurrentStopResponse? ToCurrentStop(IEnumerable<TripStop> stops)
    {
        var stop = stops.OrderBy(value => value.Seq).FirstOrDefault(value => value.Status != TripStopStatus.Done);
        return stop is null ? null : new TripCurrentStopResponse { Sequence = stop.Seq, Location = stop.Location.Code, Action = Code(stop.Action), Status = Code(stop.Status) };
    }
    private static string? CompartmentStatus(MissionStatus status) => status switch
    {
        MissionStatus.Assigned => "WAITING_FOR_PICKUP",
        MissionStatus.InProgress => "ONBOARD",
        MissionStatus.Completed => "EMPTY",
        _ => null
    };
    private static bool Valid(object query) => Validator.TryValidateObject(query, new ValidationContext(query), null, true) && query switch { PlanListQuery value => !value.From.HasValue || !value.To.HasValue || value.From <= value.To, TripListQuery value => !value.From.HasValue || !value.To.HasValue || value.From <= value.To, _ => true };
    private static bool TryPlanStatuses(string? input, out IReadOnlyList<DispatchPlanStatus>? statuses) => TryEnums(input, out statuses);
    private static bool TryTripStatuses(string? input, out IReadOnlyList<TripStatus>? statuses) => TryEnums(input, out statuses);
    private static bool TryEnums<T>(string? input, out IReadOnlyList<T>? values) where T : struct, Enum
    {
        values = null; if (string.IsNullOrWhiteSpace(input)) return true;
        var result = new List<T>();
        foreach (var text in input.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            if (!Enum.TryParse<T>(text, true, out var value) || !Enum.IsDefined(value)) return false; else result.Add(value);
        if (result.Count == 0) return false; values = result.Distinct().ToList(); return true;
    }
    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateTimeOffset Offset(DateTime value) => new(value);
    private static DateTimeOffset? Offset(DateTime? value) => value is null ? null : new DateTimeOffset(value.Value);
    private static string Code<T>(T value) where T : struct, Enum => value.ToString().ToUpperInvariant();
}
