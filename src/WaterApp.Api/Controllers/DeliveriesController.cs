using Microsoft.AspNetCore.Mvc;
using WaterApp.Api.Data;
using WaterApp.Api.Domain;

namespace WaterApp.Api.Controllers;

public record CreateDelivery(
    Guid? Id, Guid CustomerId, Guid ProductId, string DriverName,
    int DeliveredQty, int EmptiesCollected, decimal? UnitPrice,
    decimal AmountPaid, PaymentMethod Method);

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

        var customer = await db.Customers.FirstOrDefaultAsync(e => e.Id == r.CustomerId);
        var product = await db.Products.FirstOrDefaultAsync(e => e.Id == r.ProductId);
        if (customer is null || product is null)
            return BadRequest("customer သို့မဟုတ် product ရှာမတွေ့ပါ");

        var price = r.UnitPrice is > 0 ? r.UnitPrice.Value : product.DefaultPrice;
        var d = new Delivery
        {
            Id = id, CustomerId = r.CustomerId, ProductId = r.ProductId,
            DriverName = r.DriverName, DeliveredQty = r.DeliveredQty,
            EmptiesCollected = r.EmptiesCollected, UnitPrice = price,
            AmountDue = price * r.DeliveredQty, AmountPaid = r.AmountPaid,
            Method = r.Method
        };
        db.Deliveries.Add(d);

        if (r.DeliveredQty != 0)
            db.BottleTxns.Add(new BottleTxn
            {
                Id = Guid.NewGuid(), CustomerId = r.CustomerId, ProductId = r.ProductId,
                Qty = r.DeliveredQty, Type = BottleTxnType.DeliveredFull, OccurredAt = d.OccurredAt
            });
        if (r.EmptiesCollected != 0)
            db.BottleTxns.Add(new BottleTxn
            {
                Id = Guid.NewGuid(), CustomerId = r.CustomerId, ProductId = r.ProductId,
                Qty = -r.EmptiesCollected, Type = BottleTxnType.EmptyReturned, OccurredAt = d.OccurredAt
            });

        await db.SaveChangesAsync();   // တစ်ခါတည်း save = transaction တစ်ခု၊ တစ်ဝက်တစ်ပျက် မဖြစ်ဘူး
        return Created($"/api/deliveries/{d.Id}", d);
    }
}
