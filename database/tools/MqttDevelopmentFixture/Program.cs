using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Enums;
using RobotMissionMS.DataAccess.Persistence;

var connectionString = Environment.GetEnvironmentVariable("SECTION9_CONNECTION")
    ?? throw new InvalidOperationException("SECTION9_CONNECTION is required.");
await using var source = DatabaseDataSource.Create(connectionString);
var options = new DbContextOptionsBuilder<RobotMissionDbContext>().UseNpgsql(source).Options;
await using var db = new RobotMissionDbContext(options);

const string robotCode = "MED-01";
var existing = await db.Robots.SingleOrDefaultAsync(item => item.RobotCode == robotCode);
if (existing is not null)
{
    Console.WriteLine($"mqtt-fixture=exists robotCode={robotCode} id={existing.Id:D}");
    return;
}

var zone = await db.Zones.SingleOrDefaultAsync(item => item.Code == "F2" && item.IsActive)
    ?? throw new InvalidOperationException("Active F2 zone is required.");
var robotType = await db.RobotTypes.SingleOrDefaultAsync(item => item.TypeCode == "MED_CART")
    ?? throw new InvalidOperationException("MED_CART robot type is required.");
db.Robots.Add(new Robot
{
    Id = Guid.NewGuid(),
    RobotCode = robotCode,
    RobotTypeId = robotType.Id,
    ZoneId = zone.Id,
    State = RobotState.Inactive,
    IsConnected = false,
    Battery = 100,
    CreatedAt = DateTime.UtcNow
});
await db.SaveChangesAsync();
Console.WriteLine($"mqtt-fixture=created robotCode={robotCode} floor={zone.Code} type={robotType.TypeCode}");
