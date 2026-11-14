using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaterApp.Api.Data;
using WaterApp.Api.Domain;

namespace WaterApp.Api.Controllers;

public record CreateCustomer(Guid? Id, string Name, string? Phone, string? Address, decimal CreditLimit);

[ApiController]
[Route("api/customers")]
public class CustomersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<Customer>> List() =>
        await db.Customers.OrderBy(x => x.Name).ToListAsync();

    [HttpPost]
    public async Task<ActionResult<Customer>> Create(CreateCustomer r)
    {
        var id = r.Id ?? Guid.NewGuid();

        // Offline app က တူညီတဲ့ Id နဲ့ ထပ်ပို့ရင် အသစ်မဖန်တီးဘဲ ရှိပြီးသားကို ပြန်ပေး
        var existing = await db.Customers.FindAsync(id);
        if (existing is not null) return Ok(existing);

        var c = new Customer
        {
            Id = id, Name = r.Name, Phone = r.Phone,
            Address = r.Address, CreditLimit = r.CreditLimit
        };
        db.Customers.Add(c);
        await db.SaveChangesAsync();
        return Created($"/api/customers/{c.Id}", c);
    }
}
