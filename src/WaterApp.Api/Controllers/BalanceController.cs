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
        // ဗူးလက်ကျန်ကို transaction တွေကနေ တွက်
        var bottles = await db.BottleTxns
            .Where(x => x.CustomerId == id)
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Held = g.Sum(x => x.Qty) })
            .ToListAsync();

        var owed = await db.Deliveries
            .Where(x => x.CustomerId == id)
            .SumAsync(x => x.AmountDue - x.AmountPaid);

        return Ok(new { customerId = id, bottles, amountOwed = owed });
    }
}
