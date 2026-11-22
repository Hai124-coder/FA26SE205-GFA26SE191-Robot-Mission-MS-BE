using Npgsql;
using RobotMissionMS.DataAccess.Enums;

namespace RobotMissionMS.DataAccess.Persistence;

public static class DatabaseDataSource
{
    public static NpgsqlDataSource Create(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.MapEnum<AlertCategory>("public.alert_category");
        builder.MapEnum<AlertSeverity>("public.alert_severity");
        builder.MapEnum<AlertStatus>("public.alert_status");
        builder.MapEnum<IncidentPhase>("public.incident_phase");
        builder.MapEnum<IncidentCargoState>("public.incident_cargo_state");
        builder.MapEnum<IncidentAction>("public.incident_action");
        builder.MapEnum<IncidentTimelineEventType>("public.incident_timeline_event_type");
        builder.MapEnum<BatchSource>("public.batch_source");
        builder.MapEnum<BatchStatus>("public.batch_status");
        builder.MapEnum<ChargingSessionStatus>("public.charging_session_status");
        builder.MapEnum<DispatchPlanSource>("public.dispatch_plan_source");
        builder.MapEnum<DispatchPlanStatus>("public.dispatch_plan_status");
        builder.MapEnum<DispatchPlanTaskStatus>("public.dispatch_plan_task_status");
        builder.MapEnum<LocationKind>("public.location_kind");
        builder.MapEnum<MapVersionStatus>("public.map_version_status");
        builder.MapEnum<MissionFailReason>("public.mission_fail_reason");
        builder.MapEnum<MissionMode>("public.mission_mode");
        builder.MapEnum<MissionPriority>("public.mission_priority");
        builder.MapEnum<MissionStatus>("public.mission_status");
        builder.MapEnum<NodeType>("public.node_type");
        builder.MapEnum<RobotState>("public.robot_state");
        builder.MapEnum<RobotCommandOutboxStatus>("public.robot_command_outbox_status");
        builder.MapEnum<ScheduleRunStatus>("public.schedule_run_status");
        builder.MapEnum<StationStatus>("public.station_status");
        builder.MapEnum<TripStatus>("public.trip_status");
        builder.MapEnum<TripStopAction>("public.trip_stop_action");
        builder.MapEnum<TripStopStatus>("public.trip_stop_status");
        builder.MapEnum<ZoneMode>("public.zone_mode");
        return builder.Build();
    }
}

