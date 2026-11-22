using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RobotMissionMS.Api.Realtime;

[Authorize]
public sealed class OperationsHub : Hub
{
    public async Task JoinFloor(string floorCode)
    {
        var floor = floorCode?.Trim();
        if (string.IsNullOrWhiteSpace(floor)) throw new HubException("FLOOR_REQUIRED");
        var user = Context.User;
        if (user?.IsInRole("OPERATOR") == true && !string.Equals(user.FindFirst("rmms:floor")?.Value, floor, StringComparison.OrdinalIgnoreCase)) throw new HubException("FLOOR_FORBIDDEN");
        if (user?.IsInRole("OPERATOR") != true && user?.IsInRole("SUPERVISOR") != true && user?.IsInRole("ADMIN") != true) throw new HubException("FLOOR_FORBIDDEN");
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(floor));
    }
    internal static string Group(string floor) => $"floor:{floor.Trim().ToUpperInvariant()}";
}
