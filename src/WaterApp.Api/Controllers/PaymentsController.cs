using Microsoft.AspNetCore.Mvc;
using WaterApp.Api.Data;
using WaterApp.Api.Domain;
using WaterApp.Api.Infrastructure;

namespace WaterApp.Api.Controllers;

public record CreatePayment(Guid? Id, Guid CustomerId, decimal Amount, PaymentMethod Method, DateTime? OccurredAt);

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
        if (r.Method is not (PaymentMethod.Cash or PaymentMethod.MobilePay))
            return BadRequest("ပြန်ဆပ်ငွေက Cash ဒါမှမဟုတ် MobilePay ဖြစ်ရမယ်");

        var (at, timeErr) = Occurred.Resolve(r.OccurredAt);
        if (timeErr is not null) return BadRequest(timeErr);

        if (await db.Customers.FirstOrDefaultAsync(e => e.Id == r.CustomerId) is null)
            return BadRequest("customer ရှာမတွေ့ပါ");

        var p = new Payment
        {
            Id = id, CustomerId = r.CustomerId, Amount = r.Amount, Method = r.Method,
            CollectedBy = User.FindFirst("name")?.Value ?? "", OccurredAt = at
        };
        db.Payments.Add(p);
        await db.SaveChangesAsync();
        return Created($"/api/payments/{p.Id}", p);
    }
}
