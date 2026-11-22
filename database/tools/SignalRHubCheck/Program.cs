using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Persistence;

var connectionString = Required("SECTION9_CONNECTION");
var issuer = Required("SECTION12_JWT_ISSUER");
var audience = Required("SECTION12_JWT_AUDIENCE");
var signingKey = Required("SECTION12_JWT_SIGNING_KEY");
var hubUrl = Environment.GetEnvironmentVariable("SECTION12_HUB_URL") ?? "http://127.0.0.1:5228/hubs/ops";

await using var dataSource = DatabaseDataSource.Create(connectionString);
var options = new DbContextOptionsBuilder<RobotMissionDbContext>().UseNpgsql(dataSource).Options;
await using var db = new RobotMissionDbContext(options);

var f2 = await db.Zones.AsNoTracking().SingleAsync(zone => zone.Code == "F2");
var f3 = await db.Zones.AsNoTracking().SingleAsync(zone => zone.Code == "F3");
var roles = await db.Roles.AsNoTracking().Where(role => role.Name == "OPERATOR" || role.Name == "SUPERVISOR" || role.Name == "ADMIN")
    .ToDictionaryAsync(role => role.Name);
if (roles.Count != 3) throw new InvalidOperationException("Required seeded roles are missing.");

var suffix = Guid.NewGuid().ToString("N")[..12];
var users = new[]
{
    NewUser("sigop" + suffix, "SignalR test operator", roles["OPERATOR"].Id, f2.Id),
    NewUser("sigsup" + suffix, "SignalR test supervisor", roles["SUPERVISOR"].Id, null),
    NewUser("sigadm" + suffix, "SignalR test admin", roles["ADMIN"].Id, null)
};

db.Users.AddRange(users);
await db.SaveChangesAsync();

try
{
    await AssertJoinAsync(users[0], "F2", succeeds: true);
    await AssertJoinAsync(users[0], "F3", succeeds: false);
    await AssertJoinAsync(users[1], "F3", succeeds: true);
    await AssertJoinAsync(users[2], "F3", succeeds: true);
    Console.WriteLine("signalr-join-floor=verified operator-own=allow operator-other=deny supervisor=allow admin=allow cleanup=planned");
}
finally
{
    db.Users.RemoveRange(users);
    await db.SaveChangesAsync();
}

async Task AssertJoinAsync(User user, string floor, bool succeeds)
{
    var token = CreateToken(user.Id);
    await using var connection = new HubConnectionBuilder()
        .WithUrl(hubUrl, options => options.AccessTokenProvider = () => Task.FromResult<string?>(token))
        .Build();
    await connection.StartAsync();
    try
    {
        await connection.InvokeAsync("JoinFloor", floor);
        if (!succeeds) throw new InvalidOperationException($"{user.Username} unexpectedly joined {floor}.");
    }
    catch (HubException exception) when (!succeeds && exception.Message.Contains("FLOOR_FORBIDDEN", StringComparison.Ordinal))
    {
        return;
    }
    finally
    {
        await connection.StopAsync();
    }
}

string CreateToken(Guid userId)
{
    var credentials = new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(signingKey)), SecurityAlgorithms.HmacSha256);
    return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        issuer,
        audience,
        [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()), new Claim("name", "signalr-check")],
        expires: DateTime.UtcNow.AddMinutes(5),
        signingCredentials: credentials));
}

static User NewUser(string username, string fullName, Guid roleId, Guid? zoneId) => new()
{
    Id = Guid.NewGuid(), Username = username, FullName = fullName, PasswordHash = "signalr-check-not-for-login",
    IsActive = true, RoleId = roleId, ZoneId = zoneId, CreatedAt = DateTime.UtcNow
};

static string Required(string name) => Environment.GetEnvironmentVariable(name)
    ?? throw new InvalidOperationException($"{name} is required.");
