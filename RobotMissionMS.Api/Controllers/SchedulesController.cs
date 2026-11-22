using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Business.Features.Schedules.DTOs;
using RobotMissionMS.Business.Features.Schedules.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/schedules")]
[Authorize(Policy = AuthPolicies.OperationsRead)]
public sealed class SchedulesController(IScheduleService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(OperatorFloor(), cancellationToken));

    [HttpPost]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    public async Task<IActionResult> Create([FromBody] ScheduleRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetOperator(out var userId, out var floor)) return Unauthorized();
        var response = await service.CreateAsync(userId, floor, request, cancellationToken);
        return response is null ? BadRequest() : CreatedAtAction(nameof(List), response);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ScheduleRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetOperator(out var userId, out var floor)) return Unauthorized();
        var response = await service.UpdateAsync(id, userId, floor, request, cancellationToken);
        return response is null ? BadRequest() : Ok(response);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] ScheduleStatusRequest request, CancellationToken cancellationToken)
    {
        var floor = OperatorFloor();
        if (string.IsNullOrWhiteSpace(floor)) return Unauthorized();
        var result = await service.SetStatusAsync(id, floor, request.IsActive, cancellationToken);
        return result is null ? NotFound() : Ok();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var floor = OperatorFloor();
        if (string.IsNullOrWhiteSpace(floor)) return Unauthorized();
        var result = await service.DeleteAsync(id, floor, cancellationToken);
        return result switch { null => NotFound(), true => NoContent(), false => Conflict(new { type = "SCHEDULE_HAS_OPEN_RUN" }) };
    }

    [HttpPost("{id:guid}/run")]
    [Authorize(Policy = AuthPolicies.OperatorOnly)]
    public async Task<IActionResult> Run(Guid id, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, CancellationToken cancellationToken)
    {
        if (!TryGetOperator(out var userId, out var floor)) return Unauthorized();
        var result = await service.RunAsync(id, userId, floor, idempotencyKey, cancellationToken);
        if (result.IsSuccess) return Ok(result.Response);
        return result.Error switch
        {
            ScheduleRunError.InvalidIdempotencyKey => Problem(type: "IDEMPOTENCY_KEY_REQUIRED", statusCode: StatusCodes.Status400BadRequest),
            ScheduleRunError.NotFound => NotFound(),
            ScheduleRunError.RunInProgress => Conflict(new { type = "SCHEDULE_RUN_IN_PROGRESS" }),
            _ => throw new InvalidOperationException("Schedule service returned an invalid run result.")
        };
    }

    private string? OperatorFloor() => User.FindFirstValue("rmms:floor");

    private bool TryGetOperator(out Guid userId, out string floor)
    {
        floor = OperatorFloor() ?? string.Empty;
        return Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId) && !string.IsNullOrWhiteSpace(floor);
    }
}
