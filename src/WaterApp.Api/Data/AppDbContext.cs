using System.Reflection;
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
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();

    // ကျပ်ပမာဏ အားလုံး decimal(18,0)
    protected override void ConfigureConventions(ModelConfigurationBuilder c) =>
        c.Properties<decimal>().HavePrecision(18, 0);

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ITenantEntity အားလုံးကို tenant filter + client-side Id
        var method = typeof(AppDbContext).GetMethod(nameof(ConfigureTenantEntity),
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var t in b.Model.GetEntityTypes()
                     .Where(t => typeof(ITenantEntity).IsAssignableFrom(t.ClrType)).ToList())
            method.MakeGenericMethod(t.ClrType).Invoke(this, [b]);

        b.Entity<Customer>().HasIndex(x => new { x.TenantId, x.Phone });
        b.Entity<BottleTxn>().HasIndex(x => new { x.TenantId, x.CustomerId });
        b.Entity<Delivery>().HasIndex(x => new { x.TenantId, x.CustomerId });
        b.Entity<Payment>().HasIndex(x => new { x.TenantId, x.CustomerId });
        b.Entity<Settlement>().HasIndex(x => new { x.TenantId, x.DriverName, x.Date }).IsUnique();
        b.Entity<AppUser>().HasIndex(x => x.Phone).IsUnique();
    }

    private void ConfigureTenantEntity<T>(ModelBuilder b) where T : class, ITenantEntity
    {
        b.Entity<T>().HasQueryFilter(x => x.TenantId == tenant.TenantId);
        b.Entity<T>().Property<Guid>("Id").ValueGeneratedNever();
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var e in ChangeTracker.Entries<ITenantEntity>()
                     .Where(e => e.State == EntityState.Added && e.Entity.TenantId == Guid.Empty))
            e.Entity.TenantId = tenant.TenantId;
        return base.SaveChangesAsync(ct);
    }
}
