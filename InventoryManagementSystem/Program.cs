using InventoryManagementSystem.Application;
using InventoryManagementSystem.Persistence;
using InventoryManagementSystem.Services;
using InventoryManagementSystem.WebApi;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SQLite by default so a clone runs with no infrastructure to set up. The path is
// overridable through configuration for anyone who wants the file elsewhere.
var connectionString = builder.Configuration.GetConnectionString("InventoryDb")
                       ?? "Data Source=inventory.db";

builder.Services.AddDbContext<InventoryContext>(options => options.UseSqlite(connectionString));

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IStockMovementService, StockMovementService>();

var app = builder.Build();

// Apply migrations on startup, then seed. Both are safe to run repeatedly: EF skips
// migrations already applied, and the seeder is a no-op once any data exists.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<InventoryContext>();
    await context.Database.MigrateAsync();
    await SeedData.EnsureSeededAsync(context);
}

// First in the pipeline, so it catches everything downstream of it.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

/// <summary>Exposed so the test project can drive the app through WebApplicationFactory.</summary>
public partial class Program;
