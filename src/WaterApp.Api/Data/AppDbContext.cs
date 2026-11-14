using Microsoft.EntityFrameworkCore;
using WaterApp.Api.Domain;

namespace WaterApp.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant)
    : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<BottleTxn> BottleTxns => Set<BottleTxn>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Customer>().HasQueryFilter(x => x.TenantId == tenant.TenantId);
        b.Entity<Product>().HasQueryFilter(x => x.TenantId == tenant.TenantId);
        b.Entity<BottleTxn>().HasQueryFilter(x => x.TenantId == tenant.TenantId);
        b.Entity<Delivery>().HasQueryFilter(x => x.TenantId == tenant.TenantId);

        b.Entity<Customer>().Property(x => x.CreditLimit).HasPrecision(18, 0);
        b.Entity<Product>().Property(x => x.DepositAmount).HasPrecision(18, 0);
        b.Entity<Product>().Property(x => x.DefaultPrice).HasPrecision(18, 0);
        b.Entity<Delivery>().Property(x => x.UnitPrice).HasPrecision(18, 0);
        b.Entity<Delivery>().Property(x => x.AmountDue).HasPrecision(18, 0);
        b.Entity<Delivery>().Property(x => x.AmountPaid).HasPrecision(18, 0);

        b.Entity<Customer>().HasIndex(x => new { x.TenantId, x.Phone });
        b.Entity<BottleTxn>().HasIndex(x => new { x.TenantId, x.CustomerId });
        b.Entity<Delivery>().HasIndex(x => new { x.TenantId, x.CustomerId });

        b.Entity<Customer>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<Product>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<BottleTxn>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<Delivery>().Property(x => x.Id).ValueGeneratedNever();
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var e in ChangeTracker.Entries<ITenantEntity>()
                     .Where(e => e.State == EntityState.Added && e.Entity.TenantId == Guid.Empty))
            e.Entity.TenantId = tenant.TenantId;
        return base.SaveChangesAsync(ct);
    }
}
