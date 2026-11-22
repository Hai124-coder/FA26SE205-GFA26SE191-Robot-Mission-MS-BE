namespace RobotMissionMS.Api.Realtime;

public sealed record OperationsEvent(Guid EventId, DateTime OccurredAt, string Type, string? Floor, object Data);
