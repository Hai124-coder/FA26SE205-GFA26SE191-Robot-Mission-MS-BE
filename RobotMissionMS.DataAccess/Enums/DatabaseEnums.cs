using NpgsqlTypes;

namespace RobotMissionMS.DataAccess.Enums;

public enum PasswordResetRequestStatus
{
    [PgName("OPEN")]
    Open,
    [PgName("DONE")]
    Done,
    [PgName("REJECTED")]
    Rejected,
    [PgName("CANCELLED")]
    Cancelled
}

public enum AlertCategory
{
    [PgName("ROBOT")]
    Robot,
    [PgName("MISSION")]
    Mission,
    [PgName("SYSTEM")]
    System,
}

public enum AlertSeverity
{
    [PgName("INFO")]
    Info,
    [PgName("WARNING")]
    Warning,
    [PgName("CRITICAL")]
    Critical,
}

public enum AlertStatus
{
    [PgName("OPEN")]
    Open,
    [PgName("ACKNOWLEDGED")]
    Acknowledged,
    [PgName("RESOLVED")]
    Resolved,
}

public enum IncidentPhase
{
    [PgName("SELF")] Self,
    [PgName("DECIDE")] Decide,
    [PgName("HUMAN")] Human,
    [PgName("DONE")] Done,
}

public enum IncidentCargoState
{
    [PgName("ONBOARD")] Onboard,
    [PgName("PICKUP")] Pickup,
    [PgName("ROUTE")] Route,
    [PgName("NONE")] None,
}

public enum IncidentAction
{
    [PgName("ACCEPT")] Accept,
    [PgName("GO_ON")] GoOn,
    [PgName("WAIT_MORE")] WaitMore,
    [PgName("GO_PARTIAL")] GoPartial,
    [PgName("HUMAN")] Human,
    [PgName("HUMAN_DONE")] HumanDone,
    [PgName("REASSIGN")] Reassign,
    [PgName("REQUEUE")] Requeue,
    [PgName("RESUME")] Resume,
    [PgName("WAIT_CONN")] WaitConn,
    [PgName("AS_FAULT")] AsFault,
    [PgName("BROKER_UP")] BrokerUp,
    [PgName("MAINTENANCE")] Maintenance,
    [PgName("CHARGE")] Charge,
    [PgName("NOTIFY")] Notify,
}

public enum IncidentTimelineEventType
{
    [PgName("DETECTED")] Detected,
    [PgName("SUGGESTED")] Suggested,
    [PgName("ACTION_REQUESTED")] ActionRequested,
    [PgName("PHASE_CHANGED")] PhaseChanged,
    [PgName("AUTO_RESOLVED")] AutoResolved,
    [PgName("RESOLVED")] Resolved,
}

public enum BatchSource
{
    [PgName("IMPORT")]
    Import,
    [PgName("MANUAL")]
    Manual,
}

public enum BatchStatus
{
    [PgName("DRAFT")]
    Draft,
    [PgName("COMMITTED")]
    Committed,
    [PgName("DISCARDED")]
    Discarded,
}

public enum DispatchPlanStatus
{
    [PgName("DRAFT")]
    Draft,
    [PgName("RELEASED")]
    Released,
    [PgName("DISCARDED")]
    Discarded,
}

public enum DispatchPlanSource
{
    [PgName("AI")]
    Ai,
    [PgName("FALLBACK")]
    Fallback,
}

public enum DispatchPlanTaskStatus
{
    [PgName("ASSIGNED")]
    Assigned,
    [PgName("WAITING")]
    Waiting,
}

public enum ChargingSessionStatus
{
    [PgName("QUEUED")]
    Queued,
    [PgName("CHARGING")]
    Charging,
    [PgName("COMPLETED")]
    Completed,
    [PgName("INTERRUPTED")]
    Interrupted,
}

public enum ScheduleRunStatus
{
    [PgName("WAITING_FOR_ROBOT")] WaitingForRobot,
    [PgName("MISSION_CREATED")] MissionCreated,
    [PgName("LATE")] Late,
    [PgName("FAILED")] Failed,
    [PgName("CANCELLED")] Cancelled,
}

public enum LocationKind
{
    [PgName("PHARMACY")]
    Pharmacy,
    [PgName("STORAGE")]
    Storage,
    [PgName("SPECIMEN")]
    Specimen,
    [PgName("DEPT")]
    Dept,
    [PgName("WASTE_STATION")]
    WasteStation,
    [PgName("CHARGER")]
    Charger,
    [PgName("CLEAN_STATION")]
    CleanStation,
    [PgName("WASTE_POINT")]
    WastePoint,
    [PgName("CORRIDOR")]
    Corridor,
}

public enum MapVersionStatus
{
    [PgName("DRAFT")]
    Draft,
    [PgName("PUBLISHED")]
    Published,
    [PgName("ARCHIVED")]
    Archived,
}

public enum MissionFailReason
{
    [PgName("NO_RECIPIENT")]
    NoRecipient,
    [PgName("LOCATION_CLOSED")]
    LocationClosed,
    [PgName("OTHER")]
    Other,
}

public enum MissionMode
{
    [PgName("BATCH")]
    Batch,
    [PgName("SCHEDULE")]
    Schedule,
}

public enum MissionPriority
{
    [PgName("NORMAL")]
    Normal,
    [PgName("HIGH")]
    High,
    [PgName("URGENT")]
    Urgent,
}

public enum MissionStatus
{
    [PgName("PENDING")]
    Pending,
    [PgName("ASSIGNED")]
    Assigned,
    [PgName("IN_PROGRESS")]
    InProgress,
    [PgName("DELIVERY_FAILED")]
    DeliveryFailed,
    [PgName("COMPLETED")]
    Completed,
    [PgName("COMPLETED_BY_HUMAN")]
    CompletedByHuman,
    [PgName("RESCHEDULED")]
    Rescheduled,
    [PgName("CANCELLED")]
    Cancelled,
}

public enum NodeType
{
    [PgName("WAYPOINT")]
    Waypoint,
    [PgName("STOP")]
    Stop,
    [PgName("CHARGER")]
    Charger,
}

public enum RobotState
{
    [PgName("INACTIVE")]
    Inactive,
    [PgName("AVAILABLE")]
    Available,
    [PgName("ON_TRIP")]
    OnTrip,
    [PgName("CHARGING")]
    Charging,
    [PgName("ERROR")]
    Error,
    [PgName("MAINTENANCE")]
    Maintenance,
}

public enum RobotCommandOutboxStatus
{
    [PgName("PENDING")]
    Pending,
    [PgName("PROCESSING")]
    Processing,
    [PgName("SENT")]
    Sent,
    [PgName("FAILED")]
    Failed,
}

public enum StationStatus
{
    [PgName("OPERATIONAL")]
    Operational,
    [PgName("OUT_OF_SERVICE")]
    OutOfService,
}

public enum TripStatus
{
    [PgName("PLANNED")]
    Planned,
    [PgName("DISPATCHED")]
    Dispatched,
    [PgName("IN_PROGRESS")]
    InProgress,
    [PgName("COMPLETED")]
    Completed,
    [PgName("ABORTED")]
    Aborted,
}

public enum TripStopAction
{
    [PgName("PICKUP")]
    Pickup,
    [PgName("DROP")]
    Drop,
    [PgName("ROUTE_POINT")]
    RoutePoint,
    [PgName("RETURN")]
    Return,
}

public enum TripStopStatus
{
    [PgName("PENDING")]
    Pending,
    [PgName("ARRIVED")]
    Arrived,
    [PgName("DONE")]
    Done,
    [PgName("SKIPPED")]
    Skipped,
}

public enum ZoneMode
{
    [PgName("NORMAL")]
    Normal,
    [PgName("DRAINING")]
    Draining,
    [PgName("MAINTENANCE")]
    Maintenance,
}

