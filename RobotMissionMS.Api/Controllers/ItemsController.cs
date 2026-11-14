using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}
