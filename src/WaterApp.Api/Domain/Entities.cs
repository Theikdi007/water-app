namespace WaterApp.Api.Domain;

public interface ITenantEntity { Guid TenantId { get; set; } }

public interface ITenantContext { Guid TenantId { get; } }

// Dev အတွက် tenant တစ်ခုတည်း၊ နောက်မှ login token ကနေ ယူမယ်
public class DevTenantContext : ITenantContext
{
    public Guid TenantId => Guid.Parse("11111111-1111-1111-1111-111111111111");
}

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? FdaLicenceNo { get; set; }
    public DateOnly? FdaExpiry { get; set; }
}

public class Customer : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public decimal CreditLimit { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Product : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";      // 20L, 1L, 500ml
    public bool IsReturnable { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal DefaultPrice { get; set; }
}

public enum BottleTxnType { DeliveredFull = 1, EmptyReturned = 2, Adjustment = 3 }

// ဗူးလက်ကျန်ကို တိုက်ရိုက်မပြင်ဘဲ ဒီ transaction တွေကနေ တွက်မယ်
public class BottleTxn : ITenantEntity
{
    public Guid Id { get; set; }                // client က UUID ထုတ်ပေးမယ် (offline sync)
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ProductId { get; set; }
    public int Qty { get; set; }                // + = customer ဆီမှာ တိုး၊ − = လျော့
    public BottleTxnType Type { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
