using Npgsql;

var connectionString = Environment.GetEnvironmentVariable("SECTION9_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("SECTION9_CONNECTION is required and must not be printed.");
}

var root = FindRepositoryRoot();
var scriptPath = Path.Combine(root, "database", "scripts", "010_add_incident_workflow.sql");
var script = await File.ReadAllTextAsync(scriptPath);

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();
await using (var command = new NpgsqlCommand(script, connection))
{
    await command.ExecuteNonQueryAsync();
}

var enums = await CountAsync(connection, """
    SELECT count(*) FROM pg_type
    WHERE typnamespace = 'public'::regnamespace
      AND typname IN ('incident_phase', 'incident_cargo_state', 'incident_action', 'incident_timeline_event_type');
    """);
var tables = await CountAsync(connection, """
    SELECT count(*) FROM information_schema.tables
    WHERE table_schema = 'public' AND table_name IN ('AlertTimelineEntries', 'AlertMissions');
    """);
var columns = await CountAsync(connection, """
    SELECT count(*) FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'Alerts'
      AND column_name IN ('ScenarioCode', 'Phase', 'CargoState', 'SuggestedAction', 'AutoResolvedAt');
    """);
var constraints = await CountAsync(connection, """
    SELECT count(*) FROM pg_constraint
    WHERE conname IN (
      'Alerts_ScenarioCode_check', 'Alerts_AutoResolvedAt_check', 'Alerts_DonePhase_check',
      'AlertTimelineEntries_ActionRequested_check', 'AlertTimelineEntries_PhaseChanged_check',
      'AlertTimelineEntries_PayloadJson_check', 'AlertTimelineEntries_AlertId_fkey',
      'AlertTimelineEntries_ActorUserId_fkey', 'AlertMissions_AlertId_fkey', 'AlertMissions_MissionId_fkey');
    """);
var indexes = await CountAsync(connection, """
    SELECT count(*) FROM pg_indexes
    WHERE schemaname = 'public' AND indexname IN (
      'Alerts_Status_CreatedAt_idx', 'Alerts_ZoneId_Status_CreatedAt_idx', 'Alerts_TripId_Status_idx',
      'Alerts_ScenarioCode_Status_idx', 'AlertTimelineEntries_AlertId_OccurredAt_Id_idx',
      'AlertTimelineEntries_ActorUserId_OccurredAt_idx', 'AlertMissions_MissionId_CreatedAt_idx');
    """);

if (enums != 4 || tables != 2 || columns != 5 || constraints != 10 || indexes != 7)
{
    throw new InvalidOperationException($"Migration verification failed: enums={enums}, tables={tables}, columns={columns}, constraints={constraints}, indexes={indexes}.");
}

Console.WriteLine($"migration010=verified enums={enums} tables={tables} columns={columns} constraints={constraints} indexes={indexes}");

static async Task<long> CountAsync(NpgsqlConnection connection, string sql)
{
    await using var command = new NpgsqlCommand(sql, connection);
    return (long)(await command.ExecuteScalarAsync() ?? 0L);
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "RobotMissionMS.sln")))
        {
            return directory.FullName;
        }
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Repository root could not be located.");
}
