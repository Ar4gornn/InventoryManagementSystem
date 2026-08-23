using InventoryManagementSystem.Application;
using InventoryManagementSystem.Persistence;
using InventoryManagementSystem.Services;
using InventoryManagementSystem.WebApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Inventory Management API",
        Version = "v1",
        Description =
            "Products, categories, and an append-only stock movement log. Stock on hand is " +
            "always the sum of a product's movements, never a stored column. Reads are open; " +
            "anything that changes data needs an X-Api-Key header.",
    });

    // Lets Swagger UI send the key, so the mutating endpoints are usable from the browser.
    options.AddSecurityDefinition(ApiKeyMiddleware.HeaderName, new OpenApiSecurityScheme
    {
        Name = ApiKeyMiddleware.HeaderName,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Required for POST, PUT, PATCH and DELETE.",
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = ApiKeyMiddleware.HeaderName,
            },
        }] = Array.Empty<string>(),
    });
});

// SQLite by default so a clone runs with no infrastructure to set up. The path is
// overridable through configuration for anyone who wants the file elsewhere.
var connectionString = builder.Configuration.GetConnectionString("InventoryDb")
                       ?? "Data Source=inventory.db";

builder.Services.AddDbContext<InventoryContext>(options => options.UseSqlite(connectionString));

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IStockMovementService, StockMovementService>();
builder.Services.AddScoped<IProductImportService, ProductImportService>();

// No fallback key. A default would be published in this file and would silently
// become the key every deployment that forgot to set one - so the app refuses to
// start instead, and says exactly what to do.
var apiKey = builder.Configuration["Security:ApiKey"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException(
        "No API key configured. Set the environment variable " +
        "Security__ApiKey (double underscore) to a secret value before starting. " +
        "See .env.example and the README quickstart.");
}

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

// Before the endpoints, after Swagger: the docs are readable without a key, the
// writes are not.
app.UseMiddleware<ApiKeyMiddleware>(apiKey);

app.MapControllers();

app.Run();

/// <summary>Exposed so the test project can drive the app through WebApplicationFactory.</summary>
public partial class Program;
