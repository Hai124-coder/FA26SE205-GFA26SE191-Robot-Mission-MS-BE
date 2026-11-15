namespace RobotMissionMS.Api.Imports;

/// <summary>Internal result used to link a newly created draft to its inbox file atomically.</summary>
public sealed record ImportDraftResult(Guid BatchId, ImportBatchResponse Response);
