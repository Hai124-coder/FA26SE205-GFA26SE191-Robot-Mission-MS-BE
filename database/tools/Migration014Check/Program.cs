using Microsoft.Extensions.Configuration;
using Npgsql;

if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") != "Development")
    throw new InvalidOperationException("Migration 014 check only runs with DOTNET_ENVIRONMENT=Development.");

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(Program).Assembly, optional: true)
    .AddEnvironmentVariables()
    .Build();
var connectionString = configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection.");
var target = new NpgsqlConnectionStringBuilder(connectionString);
Console.WriteLine($"Target: {target.Host}:{target.Port}/{target.Database}; migration: 014");

var root = Directory.GetCurrentDirectory();
while (!File.Exists(Path.Combine(root, "RobotMissionMS.sln")))
    root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();
var migrationPath = Path.Combine(root, "database", "scripts", "014_add_user_password_workflow.sql");
await using (var apply = new NpgsqlCommand(await File.ReadAllTextAsync(migrationPath), connection))
    await apply.ExecuteNonQueryAsync();

const string verificationSql = """
SELECT
  (SELECT count(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Users' AND column_name IN ('MustChangePassword', 'LastLoginAt')) AS user_columns,
  (SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'PasswordResetRequests') AS reset_table,
  (SELECT count(*) FROM pg_type WHERE typnamespace = 'public'::regnamespace AND typname = 'password_reset_request_status') AS reset_enum,
  (SELECT count(*) FROM pg_indexes WHERE schemaname = 'public' AND indexname = 'PasswordResetRequests_UserId_Open_key') AS open_index,
  (SELECT relrowsecurity FROM pg_class WHERE oid = 'public."PasswordResetRequests"'::regclass) AS rls_enabled;
""";
await using var verify = new NpgsqlCommand(verificationSql, connection);
await using var reader = await verify.ExecuteReaderAsync();
if (!await reader.ReadAsync() || reader.GetInt64(0) != 2 || reader.GetInt64(1) != 1 || reader.GetInt64(2) != 1 || reader.GetInt64(3) != 1 || !reader.GetBoolean(4))
    throw new InvalidOperationException("Migration 014 verification failed.");

Console.WriteLine("migration014=verified userColumns=2 resetTable=1 resetEnum=1 openRequestIndex=1 rls=true");
