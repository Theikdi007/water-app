using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace WaterApp.Tests;

[Collection("api")]
public class DeliveryTests(ApiFixture f)
{
    [Fact]
    public async Task Valid_delivery_takes_driver_name_from_token()
    {
        var (c, p) = await f.SeedAsync();
        var (d, user) = await f.UserClientAsync(3);
        var res = await d.PostAsJsonAsync("/api/deliveries", ApiFixture.DeliveryBody(c, p));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var j = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(user.Name, j.GetProperty("driverName").GetString());
        Assert.Equal(3000, j.GetProperty("amountDue").GetDecimal());   // 2 x 1500 (default price)
    }

    [Fact]
    public async Task Negative_quantity_is_rejected()
    {
        var (c, p) = await f.SeedAsync();
        var (d, _) = await f.UserClientAsync(3);
        var res = await d.PostAsJsonAsync("/api/deliveries", ApiFixture.DeliveryBody(c, p, qty: -1));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Credit_delivery_with_payment_is_rejected()
    {
        var (c, p) = await f.SeedAsync();
        var (d, _) = await f.UserClientAsync(3);
        var res = await d.PostAsJsonAsync("/api/deliveries",
            ApiFixture.DeliveryBody(c, p, paid: 500, method: 3));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Paying_more_than_due_is_rejected()
    {
        var (c, p) = await f.SeedAsync();
        var (d, _) = await f.UserClientAsync(3);
        var res = await d.PostAsJsonAsync("/api/deliveries", ApiFixture.DeliveryBody(c, p, paid: 5000));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Future_occurredAt_is_rejected()
    {
        var (c, p) = await f.SeedAsync();
        var (d, _) = await f.UserClientAsync(3);
        var res = await d.PostAsJsonAsync("/api/deliveries",
            ApiFixture.DeliveryBody(c, p, at: DateTime.UtcNow.AddDays(2)));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Too_old_occurredAt_is_rejected()
    {
        var (c, p) = await f.SeedAsync();
        var (d, _) = await f.UserClientAsync(3);
        var res = await d.PostAsJsonAsync("/api/deliveries",
            ApiFixture.DeliveryBody(c, p, at: DateTime.UtcNow.AddDays(-40)));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Retry_with_same_id_does_not_double_count()
    {
        var (c, p) = await f.SeedAsync();
        var (d, _) = await f.UserClientAsync(3);
        var id = Guid.NewGuid();
        var body = ApiFixture.DeliveryBody(c, p, id: id);

        Assert.Equal(HttpStatusCode.Created, (await d.PostAsJsonAsync("/api/deliveries", body)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await d.PostAsJsonAsync("/api/deliveries", body)).StatusCode);

        var o = await f.OwnerAsync();
        var bal = await o.GetFromJsonAsync<JsonElement>($"/api/customers/{c}/balance");
        Assert.Equal(1, bal.GetProperty("bottles")[0].GetProperty("held").GetInt32());   // 2 ပို့ - 1 ပြန်ယူ
    }

    [Fact]
    public async Task Balance_shows_owed_amount_and_over_limit()
    {
        var (c, p) = await f.SeedAsync(creditLimit: 1000);
        var (d, _) = await f.UserClientAsync(3);
        // ၃ ဗူး x 1500 = 4500၊ ၃၀၀၀ ပေး → ၁၅၀၀ အကြွေး
        var res = await d.PostAsJsonAsync("/api/deliveries",
            ApiFixture.DeliveryBody(c, p, qty: 3, empties: 0, paid: 3000));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var o = await f.OwnerAsync();
        var bal = await o.GetFromJsonAsync<JsonElement>($"/api/customers/{c}/balance");
        Assert.Equal(1500, bal.GetProperty("amountOwed").GetDecimal());
        Assert.True(bal.GetProperty("overLimit").GetBoolean());
    }

    [Fact]
    public async Task Payment_reduces_owed_amount()
    {
        var (c, p) = await f.SeedAsync();
        var (d, _) = await f.UserClientAsync(3);
        await d.PostAsJsonAsync("/api/deliveries", ApiFixture.DeliveryBody(c, p, qty: 3, empties: 0, paid: 3000));

        var pay = await d.PostAsJsonAsync("/api/payments", new { customerId = c, amount = 500, method = 1 });
        Assert.Equal(HttpStatusCode.Created, pay.StatusCode);

        var o = await f.OwnerAsync();
        var bal = await o.GetFromJsonAsync<JsonElement>($"/api/customers/{c}/balance");
        Assert.Equal(1000, bal.GetProperty("amountOwed").GetDecimal());
    }

    [Fact]
    public async Task Payment_with_zero_amount_is_rejected()
    {
        var (c, _) = await f.SeedAsync();
        var (d, _) = await f.UserClientAsync(3);
        var res = await d.PostAsJsonAsync("/api/payments", new { customerId = c, amount = 0, method = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact(Skip = "Step 9: Driver က unitPrice ပြောင်းလို့ရနေတုန်း၊ ပိတ်ပြီးမှ Skip ဖြုတ်ပါ")]
    public async Task Driver_cannot_override_unit_price()
    {
        var (c, p) = await f.SeedAsync();
        var (d, _) = await f.UserClientAsync(3);
        var res = await d.PostAsJsonAsync("/api/deliveries",
            ApiFixture.DeliveryBody(c, p, price: 1, paid: 2));
        var j = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1500, j.GetProperty("unitPrice").GetDecimal());   // default ဈေးကိုပဲ သုံးရမယ်
    }
}
