using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace WaterApp.Tests;

[Collection("api")]
public class TenantTests(ApiFixture f)
{
    [Fact]
    public async Task Other_tenant_cannot_see_my_customers()
    {
        var (customerId, _) = await f.SeedAsync();
        var other = await f.OtherTenantOwnerAsync();

        var list = await other.GetFromJsonAsync<JsonElement>("/api/customers");
        Assert.DoesNotContain(list.EnumerateArray(), x => x.GetProperty("id").GetGuid() == customerId);

        var balance = await other.GetAsync($"/api/customers/{customerId}/balance");
        Assert.Equal(HttpStatusCode.NotFound, balance.StatusCode);
    }

    [Fact]
    public async Task Other_tenant_cannot_deliver_to_my_customer()
    {
        var (customerId, productId) = await f.SeedAsync();
        var other = await f.OtherTenantOwnerAsync();
        var res = await other.PostAsJsonAsync("/api/deliveries", ApiFixture.DeliveryBody(customerId, productId));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
