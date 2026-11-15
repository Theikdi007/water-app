using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WaterApp.Api.Controllers;
using WaterApp.Api.Data;
using WaterApp.Api.Domain;

namespace WaterApp.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    static ApiFactory()
    {
        // Host မတက်ခင် config ကို environment variable နဲ့ ထည့် (dev DB မထိအောင် Test DB သုံး)
        var cs = Environment.GetEnvironmentVariable("TEST_SQL")
            ?? "Server=localhost,1434;Database=WaterApp_Test;User Id=sa;Password=Water@12345;TrustServerCertificate=True";
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", cs);
        Environment.SetEnvironmentVariable("Jwt__Key", "test-only-key-0123456789-0123456789-0123456789");
        Environment.SetEnvironmentVariable("Setup__Key", ApiFixture.SetupKey);
    }
}

public record TestUser(string Name, string Phone, string Pin);

public class ApiFixture : IDisposable
{
    public const string SetupKey = "test-setup-key";
    public const string OwnerPhone = "09100000001";
    public const string OwnerPin = "1234";
    public static readonly Guid OtherTenant = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public ApiFactory Factory { get; } = new();

    public ApiFixture()
    {
        // Test DB ကို အသစ်ကနေ ပြန်တည်ဆောက် (migration တွေကိုပါ စမ်းသလိုဖြစ်)
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureDeleted();
            db.Database.Migrate();
        }
        var res = Factory.CreateClient().PostAsJsonAsync("/api/auth/bootstrap", new
        {
            tenantName = "Test Water", ownerName = "Test Owner",
            phone = OwnerPhone, pin = OwnerPin, setupKey = SetupKey
        }).GetAwaiter().GetResult();
        res.EnsureSuccessStatusCode();
    }

    public void Dispose() => Factory.Dispose();

    public HttpClient Anonymous() => Factory.CreateClient();

    public async Task<HttpClient> LoginAsync(string phone, string pin)
    {
        var c = Factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/login", new { phone, pin });
        res.EnsureSuccessStatusCode();
        var j = await res.Content.ReadFromJsonAsync<JsonElement>();
        c.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", j.GetProperty("token").GetString());
        return c;
    }

    public Task<HttpClient> OwnerAsync() => LoginAsync(OwnerPhone, OwnerPin);

    // role: 2 = Cashier, 3 = Driver
    public async Task<TestUser> CreateUserAsync(int role)
    {
        var owner = await OwnerAsync();
        var u = new TestUser("user-" + Guid.NewGuid().ToString("N")[..8],
            "09" + Random.Shared.NextInt64(100000000, 999999999), "4321");
        var res = await owner.PostAsJsonAsync("/api/users",
            new { name = u.Name, phone = u.Phone, pin = u.Pin, role });
        res.EnsureSuccessStatusCode();
        return u;
    }

    public async Task<(HttpClient Client, TestUser User)> UserClientAsync(int role)
    {
        var u = await CreateUserAsync(role);
        return (await LoginAsync(u.Phone, u.Pin), u);
    }

    // tenant အသစ်တစ်ခုရဲ့ Owner (DB ထဲ တိုက်ရိုက်ထည့်)
    public async Task<HttpClient> OtherTenantOwnerAsync()
    {
        var phone = "09" + Random.Shared.NextInt64(100000000, 999999999);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = new AppUser
            {
                Id = Guid.NewGuid(), TenantId = OtherTenant, Name = "Other Owner",
                Phone = phone, Role = UserRole.Owner
            };
            u.PinHash = AuthController.HashPin(u, "1234");
            db.AppUsers.Add(u);
            await db.SaveChangesAsync();
        }
        return await LoginAsync(phone, "1234");
    }

    // customer + product တစ်ခုစီ ဖန်တီး (default price 1500)
    public async Task<(Guid Customer, Guid Product)> SeedAsync(decimal creditLimit = 100000)
    {
        var o = await OwnerAsync();
        var cr = await o.PostAsJsonAsync("/api/customers", new
        {
            name = "Cust " + Guid.NewGuid().ToString("N")[..6], phone = "09999", address = "Yangon", creditLimit
        });
        cr.EnsureSuccessStatusCode();
        var pr = await o.PostAsJsonAsync("/api/products", new
        {
            name = "20L " + Guid.NewGuid().ToString("N")[..6], isReturnable = true,
            depositAmount = 5000, defaultPrice = 1500
        });
        pr.EnsureSuccessStatusCode();
        var c = await cr.Content.ReadFromJsonAsync<JsonElement>();
        var p = await pr.Content.ReadFromJsonAsync<JsonElement>();
        return (c.GetProperty("id").GetGuid(), p.GetProperty("id").GetGuid());
    }

    public static object DeliveryBody(Guid customer, Guid product, int qty = 2, int empties = 1,
        decimal? price = null, decimal paid = 3000, int method = 1, DateTime? at = null, Guid? id = null) =>
        new
        {
            id, customerId = customer, productId = product, deliveredQty = qty,
            emptiesCollected = empties, unitPrice = price, amountPaid = paid, method, occurredAt = at
        };
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFixture> { }
