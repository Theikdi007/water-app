namespace WaterApp.Api.Domain;

public enum PaymentMethod { Cash = 1, MobilePay = 2, Credit = 3 }

public class Delivery : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ProductId { get; set; }
    public string DriverName { get; set; } = "";   // User ဇယား မရှိသေးလို့ နာမည်ပဲ မှတ်မယ်
    public int DeliveredQty { get; set; }
    public int EmptiesCollected { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal AmountDue { get; set; }         // UnitPrice x DeliveredQty
    public decimal AmountPaid { get; set; }
    public PaymentMethod Method { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
