using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.DataAccess.Entities;

public partial class Alert
{
    public AlertCategory Category { get; set; }
    public AlertSeverity Severity { get; set; }
    public AlertStatus Status { get; set; }
    public IncidentPhase? Phase { get; set; }
    public IncidentCargoState? CargoState { get; set; }
    public IncidentAction? SuggestedAction { get; set; }
}

public partial class Batch
{
    public BatchSource Source { get; set; }
    public BatchStatus Status { get; set; }
}

public partial class ChargingSession
{
    public ChargingSessionStatus Status { get; set; }
}

public partial class ChargingStation
{
    public StationStatus Status { get; set; }
}

public partial class Location
{
    public LocationKind Kind { get; set; }
}

public partial class MapNode
{
    public NodeType NodeType { get; set; }
}

public partial class MapVersion
{
    public MapVersionStatus Status { get; set; }
}

public partial class Mission
{
    public MissionPriority Priority { get; set; }
    public MissionStatus Status { get; set; }
    public MissionFailReason? FailReason { get; set; }
}

public partial class DispatchPlan
{
    public DispatchPlanStatus Status { get; set; }
    public DispatchPlanSource Source { get; set; }
}

public partial class DispatchPlanStop
{
    public TripStopAction Action { get; set; }
}

public partial class DispatchPlanTask
{
    public DispatchPlanTaskStatus Status { get; set; }
    public MissionPriority Priority { get; set; }
}

public partial class MissionStatusHistory
{
    public MissionStatus? FromStatus { get; set; }
    public MissionStatus ToStatus { get; set; }
}

public partial class MissionType
{
    public MissionMode Mode { get; set; }
    public LocationKind? PickupKind { get; set; }
}

public partial class Robot
{
    public RobotState State { get; set; }
}

public partial class TelemetrySnapshot
{
    public RobotState State { get; set; }
}

public partial class Trip
{
    public TripStatus Status { get; set; }
}

public partial class TripStop
{
    public TripStopAction Action { get; set; }
    public TripStopStatus Status { get; set; }
}

public partial class Zone
{
    public ZoneMode OperationMode { get; set; }
}

