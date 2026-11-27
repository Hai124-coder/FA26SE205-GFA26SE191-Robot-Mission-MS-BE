using Microsoft.Extensions.Configuration;
using Npgsql;

if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") != "Development")
    throw new InvalidOperationException("Migration 015 requires DOTNET_ENVIRONMENT=Development.");

var configuration = new ConfigurationBuilder().AddUserSecrets(typeof(Program).Assembly).Build();
var connectionString = configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Development connection is missing from user secrets.");
var target = new NpgsqlConnectionStringBuilder(connectionString);
if (args is not [var expectedHost, var expectedDatabase] ||
    !string.Equals(target.Host, expectedHost, StringComparison.OrdinalIgnoreCase) || target.Database != expectedDatabase)
    throw new InvalidOperationException("Pass the approved development host and database as the two arguments.");

var root = Directory.GetCurrentDirectory();
while (!File.Exists(Path.Combine(root, "RobotMissionMS.sln")))
    root = Directory.GetParent(root)?.FullName ?? throw new InvalidOperationException("Repository root not found.");

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();
var sql = await File.ReadAllTextAsync(Path.Combine(root, "database", "scripts", "015_add_robot_lifecycle.sql"));
await using (var apply = new NpgsqlCommand(sql, connection, transaction: null) { CommandTimeout = 45 })
    await apply.ExecuteNonQueryAsync();

const string verifySql = """
SELECT count(*)
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'Robots' AND (
    (column_name = 'AwaitingFirstConnection' AND data_type = 'boolean' AND is_nullable = 'NO' AND column_default = 'false')
    OR (column_name IN ('SuspendRequestedAt', 'DecommissionedAt') AND data_type = 'timestamp with time zone' AND is_nullable = 'YES')
);
""";
await using var verify = new NpgsqlCommand(verifySql, connection);
if ((long)(await verify.ExecuteScalarAsync() ?? 0L) != 3)
    throw new InvalidOperationException("Migration 015 column verification failed.");

Console.WriteLine("migration015=verified robotLifecycleColumns=3");
