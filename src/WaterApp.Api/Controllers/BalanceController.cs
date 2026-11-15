using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaterApp.Api.Data;

namespace WaterApp.Api.Controllers;

[ApiController]
public class BalanceController(AppDbContext db) : ControllerBase
{
    [HttpGet("api/customers/{id:guid}/balance")]
    public async Task<IActionResult> Get(Guid id)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(e => e.Id == id);
        if (customer is null) return NotFound();

        var bottles = await db.BottleTxns
            .Where(x => x.CustomerId == id)
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Held = g.Sum(x => x.Qty) })
            .ToListAsync();

        var fromDeliveries = await db.Deliveries
            .Where(x => x.CustomerId == id)
            .SumAsync(x => x.AmountDue - x.AmountPaid);
        var repaid = await db.Payments
            .Where(x => x.CustomerId == id)
            .SumAsync(x => x.Amount);
        var owed = fromDeliveries - repaid;

        return Ok(new
        {
            customerId = id, customer.Name, bottles,
            amountOwed = owed, creditLimit = customer.CreditLimit,
            overLimit = owed > customer.CreditLimit
        });
    }
}
