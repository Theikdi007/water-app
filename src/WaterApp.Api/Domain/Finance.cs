namespace WaterApp.Api.Domain;

// အကြွေးအဟောင်း ပြန်ဆပ်တာ (delivery နဲ့ မဆိုင်တဲ့ ငွေရတာ)
public class Payment : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string CollectedBy { get; set; } = "";
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

// Driver တစ်ယောက်ရဲ့ တစ်နေ့စာ စာရင်းပိတ်
public class Settlement : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string DriverName { get; set; } = "";
    public DateOnly Date { get; set; }          // မြန်မာစံတော်ချိန်နေ့စွဲ
    public decimal ExpectedCash { get; set; }
    public decimal ActualCash { get; set; }
    public int ExpectedEmpties { get; set; }
    public int ActualEmpties { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
