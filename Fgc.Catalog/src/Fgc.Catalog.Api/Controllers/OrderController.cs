using Fgc.Catalog.Application.DTOS.Orders;
using Fgc.Catalog.Application.Services;
using Fgc.Catalog.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fgc.Catalog.API.Controllers;

[ApiController]
[Route("orders")]
[Tags("Orders")]
public class OrderController(OrderService orderService) : ControllerBase
{
    /// <summary>
    /// Purchase a game (creates an order and publishes OrderPlacedEvent)
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> PurchaseGame([FromBody] OrderRequest request)
    {
        try
        {
            var response = await orderService.PurchaseAsync(request.UserId, request.GameId, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(GetOrderById), new { id = response.Id }, response);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ConflictException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get an order by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetOrderById(Guid id)
    {
        var order = await orderService.GetByIdAsync(id);
        if (order == null)
            return NotFound(new { error = "Order not found" });

        return Ok(order);
    }
}
