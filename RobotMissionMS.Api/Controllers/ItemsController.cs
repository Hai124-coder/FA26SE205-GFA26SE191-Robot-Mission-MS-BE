using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RobotMissionMS.Api.Authorization;
using RobotMissionMS.Business.Features.Items.DTOs;
using RobotMissionMS.Business.Features.Items.Services;

namespace RobotMissionMS.Api.Controllers;

[ApiController]
[Route("api/v1/items")]
[Authorize]
public sealed class ItemsController(IItemCatalogService itemCatalogService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ItemResponse>>> GetItems([FromQuery] ItemListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await itemCatalogService.GetItemsAsync(query, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Response)
            : Problem(type: "ITEMS_INVALID_QUERY", title: "The item query is invalid.", statusCode: StatusCodes.Status400BadRequest);
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<ItemResponse>> CreateItem([FromBody] CreateItemRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await itemCatalogService.CreateAsync(userId, request, cancellationToken);
        if (result.IsSuccess) return Created($"/api/v1/items/{result.Response!.Code}", result.Response);
        return result.Error switch
        {
            CreateItemError.InvalidRequest => Problem(type: "ITEMS_INVALID_REQUEST", title: "The item request is invalid.", statusCode: 400),
            CreateItemError.DuplicateCode => Problem(type: "ITEMS_DUPLICATE_CODE", title: "The item code already exists.", statusCode: 409),
            CreateItemError.MissionTypeNotFound => Problem(type: "MISSION_TYPES_NOT_FOUND", title: "The mission type was not found.", statusCode: 400),
            _ => throw new InvalidOperationException("Item service returned an invalid create result.")
        };
    }

    [HttpPut("{code}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<ItemResponse>> UpdateItem(string code, [FromBody] UpdateItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await itemCatalogService.UpdateAsync(userId, code, request, cancellationToken);
        if (result.IsSuccess) return Ok(result.Response);
        return result.Error switch
        {
            UpdateItemError.InvalidRequest => Problem(type: "ITEMS_INVALID_REQUEST", title: "The item request is invalid.", statusCode: 400),
            UpdateItemError.NotFound => Problem(type: "ITEMS_NOT_FOUND", title: "The item was not found.", statusCode: 404),
            UpdateItemError.MissionTypeNotFound => Problem(type: "MISSION_TYPES_NOT_FOUND", title: "The mission type was not found.", statusCode: 400),
            UpdateItemError.MissionTypeChangeNotAllowed => Problem(type: "ITEMS_MISSION_TYPE_CHANGE_NOT_ALLOWED", title: "The mission type cannot change after the item has been used.", statusCode: 409),
            _ => throw new InvalidOperationException("Item service returned an invalid update result.")
        };
    }

    [HttpPatch("{code}/status")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult<ItemResponse>> UpdateStatus(string code, [FromBody] UpdateItemStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await itemCatalogService.UpdateStatusAsync(userId, code, request, cancellationToken);
        if (result.IsSuccess) return Ok(result.Response);
        return result.Error switch
        {
            UpdateItemStatusError.InvalidRequest => Problem(type: "ITEMS_INVALID_STATUS_REQUEST", title: "The item status request is invalid.", statusCode: 400),
            UpdateItemStatusError.NotFound => Problem(type: "ITEMS_NOT_FOUND", title: "The item was not found.", statusCode: 404),
            _ => throw new InvalidOperationException("Item service returned an invalid status result.")
        };
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
