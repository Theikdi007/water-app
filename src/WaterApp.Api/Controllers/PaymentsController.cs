using Microsoft.AspNetCore.Mvc;
using WaterApp.Api.Data;
using WaterApp.Api.Domain;

namespace WaterApp.Api.Controllers;

public record CreatePayment(Guid? Id, Guid CustomerId, decimal Amount, PaymentMethod Method, string CollectedBy);

[ApiController]
[Route("api/payments")]
public class PaymentsController(AppDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreatePayment r)
    {
        var id = r.Id ?? Guid.NewGuid();
        var existing = await db.Payments.FirstOrDefaultAsync(e => e.Id == id);
        if (existing is not null) return Ok(existing);

        if (r.Amount <= 0) return BadRequest("Amount က 0 ထက် ကြီးရမယ်");
        if (await db.Customers.FirstOrDefaultAsync(e => e.Id == r.CustomerId) is null) return BadRequest("customer ရှာမတွေ့ပါ");

        var p = new Payment
        {
            Id = id, CustomerId = r.CustomerId, Amount = r.Amount,
            Method = r.Method, CollectedBy = r.CollectedBy
        };
        db.Payments.Add(p);
        await db.SaveChangesAsync();
        return Created($"/api/payments/{p.Id}", p);
    }
}
