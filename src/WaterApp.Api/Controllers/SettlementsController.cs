using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaterApp.Api.Data;
using WaterApp.Api.Domain;

namespace WaterApp.Api.Controllers;

public record DailySummary(string Driver, DateOnly Date, int Stops, int BottlesDelivered,
    int EmptiesCollected, decimal CashCollected, decimal MobilePayCollected, decimal CreditGiven);

public record CreateSettlement(Guid? Id, string DriverName, DateOnly Date,
    decimal ActualCash, int ActualEmpties, string? Note);

[ApiController]
[Route("api/settlements")]
public class SettlementsController(AppDbContext db) : ControllerBase
{
    // မြန်မာစံတော်ချိန် UTC+6:30 နဲ့ "တစ်နေ့" ကို တွက်
    static readonly TimeSpan Mm = TimeSpan.FromHours(6.5);

    async Task<DailySummary> Compute(string driver, DateOnly date)
    {
        var start = date.ToDateTime(TimeOnly.MinValue) - Mm;
        var end = start.AddDays(1);

        var ds = await db.Deliveries
            .Where(x => x.DriverName == driver && x.OccurredAt >= start && x.OccurredAt < end)
            .ToListAsync();
        var ps = await db.Payments
            .Where(x => x.CollectedBy == driver && x.OccurredAt >= start && x.OccurredAt < end)
            .ToListAsync();

        return new DailySummary(driver, date, ds.Count,
            ds.Sum(x => x.DeliveredQty), ds.Sum(x => x.EmptiesCollected),
            ds.Where(x => x.Method == PaymentMethod.Cash).Sum(x => x.AmountPaid)
                + ps.Where(x => x.Method == PaymentMethod.Cash).Sum(x => x.Amount),
            ds.Where(x => x.Method == PaymentMethod.MobilePay).Sum(x => x.AmountPaid)
                + ps.Where(x => x.Method == PaymentMethod.MobilePay).Sum(x => x.Amount),
            ds.Sum(x => x.AmountDue - x.AmountPaid));
    }

    // ညနေစာရင်းမပိတ်ခင် မျှော်မှန်းငွေ/ဗူးခွံ ကြည့်ဖို့
    [HttpGet("summary")]
    public async Task<DailySummary> Summary([FromQuery] string driver, [FromQuery] DateOnly date) =>
        await Compute(driver, date);

    [HttpGet]
    public async Task<List<Settlement>> List([FromQuery] DateOnly date) =>
        await db.Settlements.Where(x => x.Date == date).OrderBy(x => x.DriverName).ToListAsync();

    [HttpPost, Microsoft.AspNetCore.Authorization.Authorize(Roles = "Owner,Cashier")]
    public async Task<IActionResult> Close(CreateSettlement r)
    {
        var id = r.Id ?? Guid.NewGuid();
        var existing = await db.Settlements.FirstOrDefaultAsync(e => e.Id == id);
        if (existing is not null) return Ok(existing);

        if (await db.Settlements.AnyAsync(x => x.DriverName == r.DriverName && x.Date == r.Date))
            return Conflict("ဒီ driver ရဲ့ ဒီနေ့စာရင်း ပိတ်ပြီးသားပါ");

        var sum = await Compute(r.DriverName, r.Date);   // မျှော်မှန်းတာကို server က တွက်
        var s = new Settlement
        {
            Id = id, DriverName = r.DriverName, Date = r.Date,
            ExpectedCash = sum.CashCollected, ActualCash = r.ActualCash,
            ExpectedEmpties = sum.EmptiesCollected, ActualEmpties = r.ActualEmpties,
            Note = r.Note
        };
        db.Settlements.Add(s);
        await db.SaveChangesAsync();

        return Created($"/api/settlements/{s.Id}", new
        {
            settlement = s,
            cashDifference = s.ActualCash - s.ExpectedCash,          // အနုတ် = ငွေလိုနေ
            emptiesDifference = s.ActualEmpties - s.ExpectedEmpties  // အနုတ် = ဗူးခွံလိုနေ
        });
    }
}
