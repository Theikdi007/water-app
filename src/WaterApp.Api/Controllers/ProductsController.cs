using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaterApp.Api.Data;
using WaterApp.Api.Domain;

namespace WaterApp.Api.Controllers;

public record CreateProduct(Guid? Id, string Name, bool IsReturnable, decimal DepositAmount, decimal DefaultPrice);

[ApiController]
[Route("api/products")]
public class ProductsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<Product>> List() =>
        await db.Products.OrderBy(x => x.Name).ToListAsync();

    [HttpPost]
    public async Task<ActionResult<Product>> Create(CreateProduct r)
    {
        var id = r.Id ?? Guid.NewGuid();
        var existing = await db.Products.FindAsync(id);
        if (existing is not null) return Ok(existing);

        var p = new Product
        {
            Id = id, Name = r.Name, IsReturnable = r.IsReturnable,
            DepositAmount = r.DepositAmount, DefaultPrice = r.DefaultPrice
        };
        db.Products.Add(p);
        await db.SaveChangesAsync();
        return Created($"/api/products/{p.Id}", p);
    }
}
