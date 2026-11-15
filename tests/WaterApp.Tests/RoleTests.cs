using System.Net;
using System.Net.Http.Json;

namespace WaterApp.Tests;

[Collection("api")]
public class RoleTests(ApiFixture f)
{
    static object ProductBody() => new
    {
        name = "P " + Guid.NewGuid().ToString("N")[..6], isReturnable = true,
        depositAmount = 0, defaultPrice = 1500
    };

    [Fact]
    public async Task Owner_can_create_product()
    {
        var o = await f.OwnerAsync();
        var res = await o.PostAsJsonAsync("/api/products", ProductBody());
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task Driver_cannot_create_product()
    {
        var (d, _) = await f.UserClientAsync(3);
        var res = await d.PostAsJsonAsync("/api/products", ProductBody());
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Cashier_cannot_create_product()
    {
        var (c, _) = await f.UserClientAsync(2);
        var res = await c.PostAsJsonAsync("/api/products", ProductBody());
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Cashier_can_create_customer()
    {
        var (c, _) = await f.UserClientAsync(2);
        var res = await c.PostAsJsonAsync("/api/customers",
            new { name = "C", phone = "09111", address = "x", creditLimit = 0 });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task Driver_cannot_create_customer()
    {
        var (d, _) = await f.UserClientAsync(3);
        var res = await d.PostAsJsonAsync("/api/customers",
            new { name = "C", phone = "09111", address = "x", creditLimit = 0 });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Driver_can_list_customers()
    {
        var (d, _) = await f.UserClientAsync(3);
        var res = await d.GetAsync("/api/customers");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Driver_cannot_list_or_create_users()
    {
        var (d, _) = await f.UserClientAsync(3);
        Assert.Equal(HttpStatusCode.Forbidden, (await d.GetAsync("/api/users")).StatusCode);
        var res = await d.PostAsJsonAsync("/api/users",
            new { name = "X", phone = "09222", pin = "1234", role = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Driver_cannot_list_settlements()
    {
        var (d, _) = await f.UserClientAsync(3);
        var res = await d.GetAsync("/api/settlements?date=2026-01-01");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
