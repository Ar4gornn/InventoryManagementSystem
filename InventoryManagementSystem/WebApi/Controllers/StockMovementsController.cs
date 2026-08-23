using InventoryManagementSystem.Application;
using InventoryManagementSystem.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagementSystem.WebApi.Controllers;

[ApiController]
[Route("api/products/{productId:int}")]
[Produces("application/json")]
public class StockMovementsController : ControllerBase
{
    private readonly IStockMovementService _movements;

    public StockMovementsController(IStockMovementService movements)
    {
        _movements = movements;
    }

    /// <summary>
    /// The product's full stock history, oldest first, each entry carrying the
    /// running total after it.
    /// </summary>
    [HttpGet("movements")]
    [ProducesResponseType(typeof(IReadOnlyList<StockMovementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<StockMovementDto>>> GetMovements(int productId, CancellationToken ct)
    {
        return Ok(await _movements.GetForProductAsync(productId, ct));
    }

    /// <summary>Current stock on hand for the product.</summary>
    [HttpGet("stock")]
    [ProducesResponseType(typeof(StockLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockLevelDto>> GetStock(int productId, CancellationToken ct)
    {
        return Ok(await _movements.GetStockLevelAsync(productId, ct));
    }

    /// <summary>
    /// Records a movement. Rejected with 400 if it would take stock below zero.
    /// </summary>
    [HttpPost("movements")]
    [ProducesResponseType(typeof(StockMovementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockMovementDto>> Record(
        int productId,
        CreateMovementRequest request,
        CancellationToken ct)
    {
        var created = await _movements.RecordAsync(productId, request, ct);
        return CreatedAtAction(nameof(GetMovements), new { productId }, created);
    }
}
