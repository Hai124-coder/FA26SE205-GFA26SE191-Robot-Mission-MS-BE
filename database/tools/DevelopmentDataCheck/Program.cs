using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Persistence;

var connectionString = Environment.GetEnvironmentVariable("SECTION9_CONNECTION")
    ?? throw new InvalidOperationException("SECTION9_CONNECTION is required.");
await using var source = DatabaseDataSource.Create(connectionString);
var options = new DbContextOptionsBuilder<RobotMissionDbContext>().UseNpgsql(source).Options;
await using var db = new RobotMissionDbContext(options);

var floorCodes = await db.Zones.OrderBy(item => item.Code).Select(item => item.Code).Take(10).ToListAsync();
var typeCodes = await db.RobotTypes.OrderBy(item => item.TypeCode).Select(item => item.TypeCode).Take(10).ToListAsync();
var robots = await db.Robots.OrderBy(item => item.RobotCode).Select(item => item.RobotCode).Take(10).ToListAsync();
Console.WriteLine($"development-data floors={floorCodes.Count} floorCodes={string.Join(',', floorCodes)} robotTypes={typeCodes.Count} typeCodes={string.Join(',', typeCodes)} robots={robots.Count} robotCodes={(robots.Count == 0 ? "none" : string.Join(',', robots))}");

var mqttRobot = await db.Robots.Where(item => item.RobotCode == "MED-01")
    .Select(item => new { item.IsConnected, item.Battery, item.CurrentX, item.CurrentY, item.LastHeartbeatAt })
    .SingleOrDefaultAsync();
if (mqttRobot is not null)
{
    var snapshots = await db.TelemetrySnapshots.CountAsync(item => item.Robot.RobotCode == "MED-01");
    Console.WriteLine($"mqtt-fixture robotCode=MED-01 connected={mqttRobot.IsConnected} battery={mqttRobot.Battery} x={mqttRobot.CurrentX} y={mqttRobot.CurrentY} heartbeat={mqttRobot.LastHeartbeatAt:O} snapshots={snapshots}");
}
