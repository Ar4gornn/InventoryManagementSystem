using InventoryManagementSystem.Application;
using InventoryManagementSystem.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagementSystem.WebApi.Controllers;

[ApiController]
[Route("api/categories")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categories;

    public CategoriesController(ICategoryService categories)
    {
        _categories = categories;
    }

    /// <summary>
    /// Lists categories with the number of products in each. Supports paging and a
    /// search across the name.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CategoryDto>>> Get(
        [FromQuery] CategoryQuery query,
        CancellationToken ct)
    {
        return Ok(await _categories.GetAsync(query, ct));
    }

    /// <summary>Gets one category by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> GetById(int id, CancellationToken ct)
    {
        var category = await _categories.GetByIdAsync(id, ct);
        return category is null ? Problem($"Category {id} does not exist.", statusCode: 404) : Ok(category);
    }

    /// <summary>Creates a category. Names are unique.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> Create(CategoryRequest request, CancellationToken ct)
    {
        var created = await _categories.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Renames a category or changes its description.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> Update(int id, CategoryRequest request, CancellationToken ct)
    {
        return Ok(await _categories.UpdateAsync(id, request, ct));
    }

    /// <summary>Deletes a category. Refused while any product still belongs to it.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _categories.DeleteAsync(id, ct);
        return NoContent();
    }
}
