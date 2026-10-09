using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// 1. Register DbContext (In-Memory for demonstration)
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseInMemoryDatabase("OrderDatabase"));

// 2. Register Typed HttpClient for Catalog Service communication
builder.Services.AddHttpClient<CatalogServiceClient>(client =>
{
    var catalogUrl = builder.Configuration["Services:CatalogUrl"] ?? "http://localhost:5139";
    client.BaseAddress = new Uri(catalogUrl);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Only enforce HTTPS redirection outside of development/containers
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// 3. Map Order API Endpoints

var ordersGroup = app.MapGroup("/api/orders");

// GET /api/orders - Fetch all orders
ordersGroup.MapGet("/", async (OrderDbContext db) =>
    await db.Orders.Include(o => o.Items).ToListAsync());

// GET /api/orders/{id} - Fetch specific order
ordersGroup.MapGet("/{id:int}", async (int id, OrderDbContext db) =>
    await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id) is Order order
        ? Results.Ok(order)
        : Results.NotFound(new { Message = $"Order with ID {id} not found." }));

// POST /api/orders - Place a new order with Catalog validation
ordersGroup.MapPost("/", async ([FromBody] CreateOrderRequest request, OrderDbContext db, CatalogServiceClient catalogClient) =>
{
    if (request.Items == null || !request.Items.Any())
    {
        return Results.BadRequest(new { Message = "Order must contain at least one item." });
    }

    var order = new Order
    {
        CustomerName = request.CustomerName,
        OrderDate = DateTime.UtcNow,
        Status = "Pending"
    };

    decimal totalAmount = 0m;

    foreach (var itemRequest in request.Items)
    {
        // Inter-service call: Validate item from CatalogAPI
        var catalogItem = await catalogClient.GetCatalogItemAsync(itemRequest.CatalogItemId);
        if (catalogItem == null)
        {
            return Results.BadRequest(new { Message = $"Product with ID {itemRequest.CatalogItemId} does not exist in Catalog." });
        }

        if (catalogItem.AvailableStock < itemRequest.Quantity)
        {
            return Results.BadRequest(new { Message = $"Insufficient stock for product '{catalogItem.Name}'." });
        }

        var orderItem = new OrderItem
        {
            CatalogItemId = catalogItem.Id,
            ProductName = catalogItem.Name,
            UnitPrice = catalogItem.Price,
            Quantity = itemRequest.Quantity
        };

        order.Items.Add(orderItem);
        totalAmount += orderItem.UnitPrice * orderItem.Quantity;
    }

    order.TotalAmount = totalAmount;

    db.Orders.Add(order);
    await db.SaveChangesAsync();

    return Results.Created($"/api/orders/{order.Id}", order);
});

// DELETE /api/orders/{id} - Cancel/Remove an order
ordersGroup.MapDelete("/{id:int}", async (int id, OrderDbContext db) =>
{
    var order = await db.Orders.FindAsync(id);
    if (order is null) return Results.NotFound(new { Message = "Order not found." });

    db.Orders.Remove(order);
    await db.SaveChangesAsync();
    return Results.Ok(new { Message = $"Order {id} cancelled successfully." });
});

app.Run();

//// --- Data Contracts explicitly mapped to the Catalog API Schema ---

//public record OrderRequest(int CatalogItemId, int Quantity, string CustomerId);

//public record CatalogErrorResponse(string Message);

//// Maps properties to match the CatalogItem definition
//public record CatalogItemRecord(int Id, string Name, string Description, decimal Price, int AvailableStock);

// 4. Domain & DTO Definitions

public class Order
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Pending";
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }
    public int CatalogItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public record CreateOrderRequest(string CustomerName, List<OrderItemRequest> Items);
public record OrderItemRequest(int CatalogItemId, int Quantity);

public class CatalogItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int AvailableStock { get; set; }
}

// 5. Database Context
public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
}

// 6. HTTP Client for Communicating with CatalogAPI
public class CatalogServiceClient
{
    private readonly HttpClient _httpClient;

    public CatalogServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CatalogItemDto?> GetCatalogItemAsync(int catalogItemId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/catalog/items/{catalogItemId}");
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<CatalogItemDto>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }
}