using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using RobotMissionMS.DataAccess.Persistence;
using RobotMissionMS.Seeder;

if (args.Length != 1 || args[0] is not ("--preview" or "--apply"))
{
    Console.WriteLine("Usage: dotnet run --project RobotMissionMS.Seeder -- --preview|--apply");
    Console.WriteLine("Requires DOTNET_ENVIRONMENT=Development. Preview validates inserts then rolls back.");
    return args.Length == 1 && args[0] == "--help" ? 0 : 1;
}
if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") != "Development")
{
    Console.Error.WriteLine("Seeder only runs with DOTNET_ENVIRONMENT=Development.");
    return 1;
}

try
{
    var config = new ConfigurationBuilder()
        .AddUserSecrets<ReferenceDataSeeder>(optional: true)
        .AddEnvironmentVariables().Build();
    var connectionString = config.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection (API User Secrets or environment).");
    var target = new NpgsqlConnectionStringBuilder(connectionString);
    Console.WriteLine($"Target: {target.Host}:{target.Port}/{target.Database}; mode: {args[0]}");
    await using var source = DatabaseDataSource.Create(connectionString);
    var options = new DbContextOptionsBuilder<RobotMissionDbContext>().UseNpgsql(source).Options;
    await using var db = new RobotMissionDbContext(options);
    await using var transaction = await db.Database.BeginTransactionAsync();
    // Serializes concurrent runs of this seeder until commit/rollback.
    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(726667, 1)");
    await new ReferenceDataSeeder(db).RunAsync();
    if (args[0] == "--apply")
    {
        await transaction.CommitAsync();
        Console.WriteLine("Committed reference data successfully.");
    }
    else
    {
        await transaction.RollbackAsync();
        Console.WriteLine("Preview passed. All inserts rolled back; no seed data saved.");
    }
    return 0;
}
catch (Exception ex)
{
    // Do not print connection strings, server detail or payloads containing credentials.
    var postgres = ex as PostgresException ?? ex.InnerException as PostgresException;
    Console.Error.WriteLine(postgres is not null
        ? $"Seed failed: PostgreSQL {postgres.SqlState}, constraint {postgres.ConstraintName ?? "n/a"}. Transaction rolled back."
        : ex is InvalidOperationException ? ex.Message
        : $"Seed failed ({ex.GetType().Name}). No transaction committed. Check connection/configuration.");
    return 1;
}
