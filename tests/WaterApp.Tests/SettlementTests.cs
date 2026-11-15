using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace WaterApp.Tests;

[Collection("api")]
public class SettlementTests(ApiFixture f)
{
    // မြန်မာစံတော်ချိန် (UTC+6:30) နေ့စွဲ
    static DateOnly MmDate(DateTime utc) => DateOnly.FromDateTime(utc.AddHours(6.5));

    async Task<(HttpClient Owner, string Driver, DateOnly Date)> DeliverTwoBottlesAsync()
    {
        var (c, p) = await f.SeedAsync();
        var (d, user) = await f.UserClientAsync(3);
        var at = DateTime.UtcNow.AddMinutes(-30);
        var res = await d.PostAsJsonAsync("/api/deliveries", ApiFixture.DeliveryBody(c, p, at: at));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await f.OwnerAsync(), user.Name, MmDate(at));
    }

    [Fact]
    public async Task Summary_counts_the_days_deliveries()
    {
        var (o, driver, date) = await DeliverTwoBottlesAsync();
        var s = await o.GetFromJsonAsync<JsonElement>(
            $"/api/settlements/summary?driver={Uri.EscapeDataString(driver)}&date={date:yyyy-MM-dd}");
        Assert.Equal(1, s.GetProperty("stops").GetInt32());
        Assert.Equal(2, s.GetProperty("bottlesDelivered").GetInt32());
        Assert.Equal(1, s.GetProperty("emptiesCollected").GetInt32());
        Assert.Equal(3000, s.GetProperty("cashCollected").GetDecimal());
    }

    [Fact]
    public async Task Closing_computes_cash_and_empties_difference()
    {
        var (o, driver, date) = await DeliverTwoBottlesAsync();
        var res = await o.PostAsJsonAsync("/api/settlements",
            new { driverName = driver, date, actualCash = 2800, actualEmpties = 1 });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var j = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(-200, j.GetProperty("cashDifference").GetDecimal());      // ငွေ ၂၀၀ လိုနေ
        Assert.Equal(0, j.GetProperty("emptiesDifference").GetInt32());
    }

    [Fact]
    public async Task Closing_the_same_day_twice_returns_409()
    {
        var (o, driver, date) = await DeliverTwoBottlesAsync();
        var body = new { driverName = driver, date, actualCash = 3000, actualEmpties = 1 };
        Assert.Equal(HttpStatusCode.Created, (await o.PostAsJsonAsync("/api/settlements", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await o.PostAsJsonAsync("/api/settlements", body)).StatusCode);
    }

    [Fact]
    public async Task Driver_cannot_close_settlements()
    {
        var (d, user) = await f.UserClientAsync(3);
        var res = await d.PostAsJsonAsync("/api/settlements", new
        {
            driverName = user.Name, date = MmDate(DateTime.UtcNow), actualCash = 0, actualEmpties = 0
        });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Driver_summary_always_shows_own_data()
    {
        var (d, user) = await f.UserClientAsync(3);
        var date = MmDate(DateTime.UtcNow);
        var s = await d.GetFromJsonAsync<JsonElement>(
            $"/api/settlements/summary?driver=someone-else&date={date:yyyy-MM-dd}");
        Assert.Equal(user.Name, s.GetProperty("driver").GetString());
    }
}
