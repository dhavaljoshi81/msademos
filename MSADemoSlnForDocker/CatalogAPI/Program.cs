using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;


var builder = WebApplication.CreateBuilder(args);

// 1. Dependency Injection: Register the In-Memory Database Context
builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseInMemoryDatabase("CatalogDatabase"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// 2. Data Seeding Pipeline: Automatically populate database on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    if (builder.Configuration.GetValue<bool>("CatalogSettings:EnableAutomaticSeeding"))
    {
        CatalogDataSeeder.Seed(context);
    }
}

// 3. REST API Endpoints Route Group Mapping

var catalogApi = app.MapGroup("/api/catalog/items");

// GET /api/catalog/items - Fetch all products
catalogApi.MapGet("/", async (CatalogDbContext db) =>
    await db.CatalogItems.ToListAsync());

// GET /api/catalog/items/{id} - Fetch a single product (Used by the Ordering API for validation)
catalogApi.MapGet("/{id:int}", async (int id, CatalogDbContext db) =>
    await db.CatalogItems.FindAsync(id) is CatalogItem item
        ? Results.Ok(item)
        : Results.NotFound(new { Message = $"Product with ID {id} not found." }));

// POST /api/catalog/items - Create a new product entry
catalogApi.MapPost("", async ([FromBody] CatalogItem newItem, CatalogDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(newItem.Name) || newItem.Price < 0)
    {
        return Results.BadRequest(new { Message = "Invalid item structure data provided." });
    }

    db.CatalogItems.Add(newItem);
    await db.SaveChangesAsync();

    return Results.Created($"/api/catalog/items/{newItem.Id}", newItem);
});

// PUT /api/catalog/items/{id} - Update an existing product
catalogApi.MapPut("/{id:int}", async (int id, [FromBody] CatalogItem updatedItem, CatalogDbContext db) =>
{
    var existingItem = await db.CatalogItems.FindAsync(id);
    if (existingItem is null) return Results.NotFound(new { Message = "Product target missing." });

    existingItem.Name = updatedItem.Name;
    existingItem.Description = updatedItem.Description;
    existingItem.Price = updatedItem.Price;
    existingItem.AvailableStock = updatedItem.AvailableStock;

    await db.SaveChangesAsync();
    return Results.NoContent();
});

// DELETE /api/catalog/items/{id} - Remove a product from the database
catalogApi.MapDelete("/{id:int}", async (int id, CatalogDbContext db) =>
{
    if (await db.CatalogItems.FindAsync(id) is CatalogItem item)
    {
        db.CatalogItems.Remove(item);
        await db.SaveChangesAsync();
        return Results.Ok(new { Message = "Item removed successfully." });
    }

    return Results.NotFound(new { Message = "Target item not located." });
});

app.Run();

// 4. Infrastructure Data Classes & Domain Definitions

public class CatalogItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int AvailableStock { get; set; }
}

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
}

public static class CatalogDataSeeder
{
    public static void Seed(CatalogDbContext context)
    {
        context.Database.EnsureCreated();

        if (!context.CatalogItems.Any())
        {
            context.CatalogItems.AddRange(
                new List<CatalogItem>
                {
                    new CatalogItem { Id = 1, Name = ".NET Microservices E-Book", Description = "A comprehensive cloud native architect handbook.", Price = 19.99m, AvailableStock = 150 },
                    new CatalogItem { Id = 2, Name = "Developer Coffee Mug", Description = "Premium thermal ceramic container for daily fuel.", Price = 14.50m, AvailableStock = 85 },
                    new CatalogItem { Id = 3, Name = "Mechanical Coding Keyboard", Description = "Compact clicky layout optimized for visual code cycles.", Price = 89.00m, AvailableStock = 30 }
                }
            );
            context.SaveChanges();
        }
    }
}
