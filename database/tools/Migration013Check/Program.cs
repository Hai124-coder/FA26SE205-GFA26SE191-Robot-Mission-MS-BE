using Npgsql;

var connectionString = Environment.GetEnvironmentVariable("SECTION9_CONNECTION")
    ?? throw new InvalidOperationException("SECTION9_CONNECTION is required.");

var root = Directory.GetCurrentDirectory();
while (!File.Exists(Path.Combine(root, "RobotMissionMS.sln")))
    root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();

if (args.Contains("--preflight", StringComparer.OrdinalIgnoreCase))
{
    const string preflightSql = """
    SELECT count(*)
    FROM public."RobotCommandOutbox"
    WHERE "CommandType" <> 'DISPATCH_TRIP'
       OR "TripId" IS NULL
       OR "PlanId" IS NULL
       OR "Topic" !~ '^rmms/v1/[^/]+/order$';
    """;
    await using var preflight = new NpgsqlCommand(preflightSql, connection);
    var incompatibleRows = Convert.ToInt64(await preflight.ExecuteScalarAsync());
    Console.WriteLine($"migration013-preflight incompatibleExistingRows={incompatibleRows}");
    return;
}

var migrationPath = Path.Combine(root, "database", "scripts", "013_add_robot_control_commands.sql");
await using (var apply = new NpgsqlCommand(await File.ReadAllTextAsync(migrationPath), connection))
    await apply.ExecuteNonQueryAsync();

const string verificationSql = """
SELECT
  (SELECT count(*) FROM information_schema.columns
   WHERE table_schema = 'public' AND table_name = 'RobotCommandOutbox'
     AND column_name IN ('TripId', 'PlanId') AND is_nullable = 'YES') AS nullable_columns,
  (SELECT count(*) FROM pg_constraint
   WHERE conrelid = 'public."RobotCommandOutbox"'::regclass
     AND conname IN ('RobotCommandOutbox_CommandType_check', 'RobotCommandOutbox_CommandShape_check')) AS constraints,
  (SELECT count(*) FROM pg_constraint
   WHERE conrelid = 'public."RobotCommandOutbox"'::regclass
     AND conname = 'RobotCommandOutbox_CommandType_check'
     AND pg_get_constraintdef(oid) LIKE '%STOP%'
     AND pg_get_constraintdef(oid) LIKE '%RESUME%') AS command_types;
""";

await using var verify = new NpgsqlCommand(verificationSql, connection);
await using var reader = await verify.ExecuteReaderAsync();
if (!await reader.ReadAsync() || reader.GetInt64(0) != 2 || reader.GetInt64(1) != 2 || reader.GetInt64(2) != 1)
    throw new InvalidOperationException("Migration 013 verification failed.");

Console.WriteLine("migration013=verified nullableColumns=2 constraints=2 commandTypes=STOP,RESUME");
