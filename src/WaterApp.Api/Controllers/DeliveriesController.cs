using Microsoft.AspNetCore.Mvc;
using WaterApp.Api.Data;
using WaterApp.Api.Domain;
using WaterApp.Api.Infrastructure;

namespace WaterApp.Api.Controllers;

public record CreateDelivery(
    Guid? Id, Guid CustomerId, Guid ProductId,
    int DeliveredQty, int EmptiesCollected, decimal? UnitPrice,
    decimal AmountPaid, PaymentMethod Method, DateTime? OccurredAt);

[ApiController]
[Route("api/deliveries")]
public class DeliveriesController(AppDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateDelivery r)
    {
        var id = r.Id ?? Guid.NewGuid();

        // Offline sync ကြောင့် ထပ်ပို့လာရင် ထပ်မမှတ်ဘဲ ရှိပြီးသားကို ပြန်ပေး
        var existing = await db.Deliveries.FirstOrDefaultAsync(e => e.Id == id);
        if (existing is not null) return Ok(existing);

        // --- validation ---
        if (r.DeliveredQty < 0 || r.EmptiesCollected < 0)
            return BadRequest("အရေအတွက် အနုတ် မဖြစ်ရပါ");
        if (r.DeliveredQty == 0 && r.EmptiesCollected == 0)
            return BadRequest("ပို့တဲ့ ဗူး ဒါမှမဟုတ် ပြန်ယူတဲ့ ဗူးခွံ တစ်ခုခု ရှိရမယ်");
        if (r.AmountPaid < 0) return BadRequest("AmountPaid အနုတ် မဖြစ်ရပါ");
        if (r.UnitPrice is < 0) return BadRequest("UnitPrice အနုတ် မဖြစ်ရပါ");
        if (!Enum.IsDefined(r.Method)) return BadRequest("Method မမှန်ပါ");
        if (r.Method == PaymentMethod.Credit && r.AmountPaid > 0)
            return BadRequest("အကြွေး (Credit) ဆိုရင် AmountPaid က 0 ဖြစ်ရမယ်");

        var (at, timeErr) = Occurred.Resolve(r.OccurredAt);
        if (timeErr is not null) return BadRequest(timeErr);

        var customer = await db.Customers.FirstOrDefaultAsync(e => e.Id == r.CustomerId);
        var product = await db.Products.FirstOrDefaultAsync(e => e.Id == r.ProductId);
        if (customer is null || product is null)
            return BadRequest("customer သို့မဟုတ် product ရှာမတွေ့ပါ");

        var price = r.UnitPrice is > 0 ? r.UnitPrice.Value : product.DefaultPrice;
        var due = price * r.DeliveredQty;
        if (r.AmountPaid > due) return BadRequest("AmountPaid က ပေးရမယ့်ငွေထက် များနေတယ်");

        // Driver နာမည်ကို token ထဲကနေပဲ ယူ (client ပို့တာ မယုံ)
        var driver = User.FindFirst("name")?.Value ?? "";

        var d = new Delivery
        {
            Id = id, CustomerId = r.CustomerId, ProductId = r.ProductId,
            DriverName = driver, DeliveredQty = r.DeliveredQty,
            EmptiesCollected = r.EmptiesCollected, UnitPrice = price,
            AmountDue = due, AmountPaid = r.AmountPaid,
            Method = r.Method, OccurredAt = at
        };
        db.Deliveries.Add(d);

        if (r.DeliveredQty != 0)
            db.BottleTxns.Add(new BottleTxn
            {
                Id = Guid.NewGuid(), CustomerId = r.CustomerId, ProductId = r.ProductId,
                Qty = r.DeliveredQty, Type = BottleTxnType.DeliveredFull, OccurredAt = at
            });
        if (r.EmptiesCollected != 0)
            db.BottleTxns.Add(new BottleTxn
            {
                Id = Guid.NewGuid(), CustomerId = r.CustomerId, ProductId = r.ProductId,
                Qty = -r.EmptiesCollected, Type = BottleTxnType.EmptyReturned, OccurredAt = at
            });

        await db.SaveChangesAsync();   // တစ်ခါတည်း save = transaction တစ်ခု
        return Created($"/api/deliveries/{d.Id}", d);
    }
}
