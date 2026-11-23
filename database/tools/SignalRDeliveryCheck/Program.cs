using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using RobotMissionMS.Api.Realtime;
using RobotMissionMS.Business.Features.Tasks.Services;

var url = "http://127.0.0.1:5230";
var builder = WebApplication.CreateBuilder();
builder.WebHost.UseUrls(url);
builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("test", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IOperationsRealtimePublisher, OperationsRealtimePublisher>();
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<OperationsHub>("/hubs/ops");
await app.StartAsync();

try
{
    await using var f2 = Connect("operator-f2");
    await using var f3 = Connect("supervisor");
    var f2Trip = new TaskCompletionSource<OperationsEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
    var f3Trip = new TaskCompletionSource<OperationsEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
    var f2System = new TaskCompletionSource<OperationsEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
    var f3System = new TaskCompletionSource<OperationsEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
    var f2Task = new TaskCompletionSource<OperationsEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
    var f3Task = new TaskCompletionSource<OperationsEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
    var f2Queue = new TaskCompletionSource<OperationsEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
    var f2Robot = new TaskCompletionSource<OperationsEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
    var f3Robot = new TaskCompletionSource<OperationsEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
    f2.On<OperationsEvent>("trip.updated", item => f2Trip.TrySetResult(item));
    f3.On<OperationsEvent>("trip.updated", item => f3Trip.TrySetResult(item));
    f2.On<OperationsEvent>("event.logged", item => f2System.TrySetResult(item));
    f3.On<OperationsEvent>("event.logged", item => f3System.TrySetResult(item));
    f2.On<OperationsEvent>("task.updated", item => f2Task.TrySetResult(item));
    f3.On<OperationsEvent>("task.updated", item => f3Task.TrySetResult(item));
    f2.On<OperationsEvent>("queue.changed", item => f2Queue.TrySetResult(item));
    f2.On<OperationsEvent>("robot.updated", item => f2Robot.TrySetResult(item));
    f3.On<OperationsEvent>("robot.updated", item => f3Robot.TrySetResult(item));
    await f2.StartAsync(); await f3.StartAsync();
    await f2.InvokeAsync("JoinFloor", "F2"); await f3.InvokeAsync("JoinFloor", "F3");

    var publisher = app.Services.GetRequiredService<IOperationsRealtimePublisher>();
    await publisher.PublishAsync("trip.updated", "F2", new { tripId = Guid.NewGuid(), idx = 1, stops = Array.Empty<object>(), slots = Array.Empty<object>() });
    var receivedTrip = await f2Trip.Task.WaitAsync(TimeSpan.FromSeconds(3));
    if (receivedTrip.Type != "trip.updated" || receivedTrip.Floor != "F2") throw new InvalidOperationException("F2 payload is incorrect.");
    if (await ReceivedWithinAsync(f3Trip.Task, TimeSpan.FromMilliseconds(750))) throw new InvalidOperationException("F3 received an F2 trip event.");

    var missions = new OperationsMissionRealtimePublisher(publisher);
    var missionId = Guid.NewGuid();
    await missions.PublishTaskUpdatedAsync("F2", new MissionRealtimeUpdate(missionId, "CANCELLED", null, null, "test"));
    await missions.PublishQueueChangedAsync("F2", 3);
    if ((await f2Task.Task.WaitAsync(TimeSpan.FromSeconds(3))).Type != "task.updated" || (await f2Queue.Task.WaitAsync(TimeSpan.FromSeconds(3))).Type != "queue.changed") throw new InvalidOperationException("Mission payload is incorrect.");
    if (await ReceivedWithinAsync(f3Task.Task, TimeSpan.FromMilliseconds(750))) throw new InvalidOperationException("F3 received an F2 task event.");

    var robots = new OperationsRobotRealtimePublisher(publisher);
    await robots.PublishUpdatedAsync("F2", new RobotRealtimeUpdate(Guid.NewGuid(), "F2", 2.5, 1.2, 92, "MOVING", true, null, "F2-N02", 1, DateTime.UtcNow));
    if ((await f2Robot.Task.WaitAsync(TimeSpan.FromSeconds(3))).Type != "robot.updated") throw new InvalidOperationException("Robot payload is incorrect.");
    if (await ReceivedWithinAsync(f3Robot.Task, TimeSpan.FromMilliseconds(750))) throw new InvalidOperationException("F3 received an F2 robot event.");

    await publisher.PublishAsync("event.logged", null, new { action = "TEST" });
    await Task.WhenAll(f2System.Task.WaitAsync(TimeSpan.FromSeconds(3)), f3System.Task.WaitAsync(TimeSpan.FromSeconds(3)));
    Console.WriteLine("signalr-delivery=verified floor-targeted=F2-only trip-task-queue-robot=verified system-event=all-clients");
}
finally
{
    await app.StopAsync();
    await app.DisposeAsync();
}

HubConnection Connect(string token) => new HubConnectionBuilder().WithUrl(url + "/hubs/ops", options => options.AccessTokenProvider = () => Task.FromResult<string?>(token)).Build();
static async Task<bool> ReceivedWithinAsync(Task task, TimeSpan timeout) => await Task.WhenAny(task, Task.Delay(timeout)) == task;

public sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = Request.Query["access_token"].ToString();
        if (string.IsNullOrWhiteSpace(token) && Request.Headers.Authorization.ToString() is { } authorization && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            token = authorization[7..];
        var claims = token switch
        {
            "operator-f2" => new[] { new Claim(ClaimTypes.Role, "OPERATOR"), new Claim("rmms:floor", "F2") },
            "supervisor" => new[] { new Claim(ClaimTypes.Role, "SUPERVISOR") },
            _ => []
        };
        if (claims.Length == 0) return Task.FromResult(AuthenticateResult.Fail("Unknown test token."));
        var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.Name, ClaimTypes.Role);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
