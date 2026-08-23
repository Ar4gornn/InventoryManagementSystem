using InventoryManagementSystem.Application;
using InventoryManagementSystem.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagementSystem.WebApi.Controllers;

[ApiController]
[Route("api/products")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _products;
    private readonly IProductImportService _import;

    public ProductsController(IProductService products, IProductImportService import)
    {
        _products = products;
        _import = import;
    }

    /// <summary>
    /// Lists products with their current stock on hand. Supports paging, a search
    /// across SKU and name, and filtering by category.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductDto>>> Get(
        [FromQuery] ProductQuery query,
        CancellationToken ct)
    {
        return Ok(await _products.GetAsync(query, ct));
    }

    /// <summary>
    /// Imports products from a CSV file with the header
    /// <c>sku,name,description,category,quantity</c>. Rows are independent: valid rows
    /// are imported and the response lists which lines failed and why.
    /// </summary>
    [HttpPost("import")]
    [ProducesResponseType(typeof(ImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportResult>> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw new DomainException("Upload a CSV file in a form field named 'file'.");
        }

        await using var stream = file.OpenReadStream();
        return Ok(await _import.ImportAsync(stream, ct));
    }

    /// <summary>Gets one product by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(int id, CancellationToken ct)
    {
        // Thrown rather than returned so every error in this API leaves through
        // the same middleware, with one shape and one content type. Returning
        // Problem() here produced application/json, because [Produces] overrides
        // it, while the middleware produced application/problem+json.
        var product = await _products.GetByIdAsync(id, ct)
                      ?? throw DomainException.NotFound($"Product {id} does not exist.");

        return Ok(product);
    }

    /// <summary>Creates a product. The SKU must not already be in use.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDto>> Create(CreateProductRequest request, CancellationToken ct)
    {
        var created = await _products.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates a product's name, description or category. The SKU is immutable.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> Update(int id, UpdateProductRequest request, CancellationToken ct)
    {
        return Ok(await _products.UpdateAsync(id, request, ct));
    }

    /// <summary>Deletes a product. Refused if it has any stock movements.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _products.DeleteAsync(id, ct);
        return NoContent();
    }
}
