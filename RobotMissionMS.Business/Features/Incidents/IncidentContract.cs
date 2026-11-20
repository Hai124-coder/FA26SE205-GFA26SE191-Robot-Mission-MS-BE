using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.Business.Features.Incidents;

public sealed record IncidentRule(string ScenarioCode, string AlertType, AlertCategory Category, AlertSeverity Severity, IncidentCargoState CargoState, IncidentPhase InitialPhase, IncidentAction? SuggestedAction, IReadOnlySet<IncidentAction> AllowedActions);

public static class IncidentContract
{
    private static readonly IReadOnlySet<IncidentAction> A1Actions = new HashSet<IncidentAction> { IncidentAction.GoOn, IncidentAction.WaitMore };
    private static readonly IReadOnlySet<IncidentAction> HumanActions = new HashSet<IncidentAction> { IncidentAction.Human, IncidentAction.Reassign, IncidentAction.Requeue };
    private static readonly IReadOnlySet<IncidentAction> FaultActions = new HashSet<IncidentAction> { IncidentAction.Resume, IncidentAction.AsFault };
    public static readonly IReadOnlyDictionary<string, IncidentRule> Rules = new Dictionary<string, IncidentRule>(StringComparer.OrdinalIgnoreCase)
    {
        ["A1"] = new("A1", "NOT_RECEIVED", AlertCategory.Mission, AlertSeverity.Warning, IncidentCargoState.Onboard, IncidentPhase.Decide, IncidentAction.GoOn, A1Actions),
        ["A4"] = new("A4", "WRONG_SLOT", AlertCategory.Mission, AlertSeverity.Critical, IncidentCargoState.Onboard, IncidentPhase.Decide, IncidentAction.Human, HumanActions),
        ["A5"] = new("A5", "PICKUP_SHORT", AlertCategory.Mission, AlertSeverity.Warning, IncidentCargoState.Pickup, IncidentPhase.Decide, IncidentAction.WaitMore, new HashSet<IncidentAction> { IncidentAction.WaitMore, IncidentAction.GoPartial }),
        ["B1"] = new("B1", "FAULT", AlertCategory.Robot, AlertSeverity.Critical, IncidentCargoState.Onboard, IncidentPhase.Decide, IncidentAction.Reassign, HumanActions),
        ["B2"] = new("B2", "FAULT", AlertCategory.Robot, AlertSeverity.Critical, IncidentCargoState.Route, IncidentPhase.Decide, IncidentAction.Human, HumanActions),
        ["B4"] = new("B4", "OBSTACLE", AlertCategory.Robot, AlertSeverity.Warning, IncidentCargoState.Route, IncidentPhase.Decide, IncidentAction.Resume, FaultActions),
        ["B5"] = new("B5", "DISCONNECTED", AlertCategory.Robot, AlertSeverity.Critical, IncidentCargoState.Route, IncidentPhase.Decide, IncidentAction.WaitConn, new HashSet<IncidentAction> { IncidentAction.WaitConn, IncidentAction.AsFault }),
        ["B6"] = new("B6", "LOW_BATTERY", AlertCategory.Robot, AlertSeverity.Warning, IncidentCargoState.Route, IncidentPhase.Decide, IncidentAction.Notify, new HashSet<IncidentAction> { IncidentAction.Notify, IncidentAction.AsFault }),
        ["E1"] = new("E1", "SCHEDULE_LATE", AlertCategory.System, AlertSeverity.Warning, IncidentCargoState.None, IncidentPhase.Decide, IncidentAction.Notify, new HashSet<IncidentAction> { IncidentAction.Notify }),
        ["E3"] = new("E3", "AI_DOWN", AlertCategory.System, AlertSeverity.Warning, IncidentCargoState.None, IncidentPhase.Decide, IncidentAction.Notify, new HashSet<IncidentAction> { IncidentAction.Notify }),
        ["E4"] = new("E4", "BROKER_DOWN", AlertCategory.System, AlertSeverity.Critical, IncidentCargoState.None, IncidentPhase.Decide, IncidentAction.BrokerUp, new HashSet<IncidentAction> { IncidentAction.BrokerUp })
    };
}
