using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

var connectionString = Environment.GetEnvironmentVariable("SECTION9_CONNECTION")
    ?? throw new InvalidOperationException("SECTION9_CONNECTION is required.");
var runId = args.FirstOrDefault(value => value.StartsWith("--run-id=", StringComparison.Ordinal))?[9..]
    ?? throw new InvalidOperationException("--run-id is required.");
var existingRobotCode = args.FirstOrDefault(value => value.StartsWith("--existing-robot-code=", StringComparison.Ordinal))?[22..];
var cleanup = args.Contains("--cleanup", StringComparer.OrdinalIgnoreCase);

await using var dataSource = DatabaseDataSource.Create(connectionString);
var options = new DbContextOptionsBuilder<RobotMissionDbContext>().UseNpgsql(dataSource).Options;
await using var db = new RobotMissionDbContext(options);

var username = $"http_{runId}";
if (cleanup)
{
    var existingUser = await db.Users.SingleOrDefaultAsync(value => value.Username == username);
    var existingRobot = string.IsNullOrWhiteSpace(existingRobotCode)
        ? await db.Robots.SingleOrDefaultAsync(value => value.RobotCode == $"HTTP-{runId}")
        : null;
    if (existingRobot is not null)
    {
        await db.RobotCommandOutbox.Where(value => value.RobotId == existingRobot.Id).ExecuteDeleteAsync();
        await db.Robots.Where(value => value.Id == existingRobot.Id).ExecuteDeleteAsync();
    }
    if (existingUser is not null)
    {
        await db.RefreshTokens.Where(value => value.UserId == existingUser.Id).ExecuteDeleteAsync();
        await db.ActivityLogs.Where(value => value.UserId == existingUser.Id).ExecuteDeleteAsync();
        await db.Users.Where(value => value.Id == existingUser.Id).ExecuteDeleteAsync();
    }
    if (string.IsNullOrWhiteSpace(existingRobotCode))
    {
        await db.RobotTypes.Where(value => value.TypeCode == $"HT{runId}").ExecuteDeleteAsync();
        await db.Zones.Where(value => value.Code == $"HT{runId}").ExecuteDeleteAsync();
    }
    Console.WriteLine($"robot-control-http-fixture=cleaned runId={runId}");
    return;
}

if (await db.Users.AnyAsync(value => value.Username == username))
    throw new InvalidOperationException("Fixture already exists; clean it before recreating.");

var adminRole = await db.Roles.SingleOrDefaultAsync(value => value.Name == "ADMIN")
    ?? throw new InvalidOperationException("An ADMIN role is required for the HTTP fixture.");
var now = DateTime.UtcNow;
var user = new User
{
    Id = Guid.NewGuid(), Username = username, FullName = "HTTP command test", IsActive = true,
    RoleId = adminRole.Id, CreatedAt = now,
};
user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "RobotControlTest!2026");
Robot robot;
if (!string.IsNullOrWhiteSpace(existingRobotCode))
{
    robot = await db.Robots.SingleOrDefaultAsync(value => value.RobotCode == existingRobotCode)
        ?? throw new InvalidOperationException($"Robot {existingRobotCode} was not found.");
    db.Add(user);
}
else
{
    var zone = new Zone { Id = Guid.NewGuid(), Code = $"HT{runId}", Name = "HTTP command test", IsActive = true };
    var type = new RobotType { Id = Guid.NewGuid(), TypeCode = $"HT{runId}", Name = "HTTP command test", Slots = 0, ColorHex = "#000000" };
    robot = new Robot
    {
        Id = Guid.NewGuid(), RobotCode = $"HTTP-{runId}", RobotTypeId = type.Id, ZoneId = zone.Id,
        IsConnected = true, LastHeartbeatAt = now, Battery = 100, State = RobotState.Available, CreatedAt = now,
    };
    db.AddRange(zone, type, user, robot);
}
await db.SaveChangesAsync();
Console.WriteLine($"{{\"username\":\"{username}\",\"password\":\"RobotControlTest!2026\",\"robotId\":\"{robot.Id:D}\",\"robotCode\":\"{robot.RobotCode}\"}}");
