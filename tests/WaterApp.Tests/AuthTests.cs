using System.Net;
using System.Net.Http.Json;

namespace WaterApp.Tests;

[Collection("api")]
public class AuthTests(ApiFixture f)
{
    [Fact]
    public async Task Login_with_wrong_pin_returns_401()
    {
        var res = await f.Anonymous().PostAsJsonAsync("/api/auth/login",
            new { phone = ApiFixture.OwnerPhone, pin = "0000" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Request_without_token_returns_401()
    {
        var res = await f.Anonymous().GetAsync("/api/customers");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Five_wrong_pins_lock_the_account()
    {
        var u = await f.CreateUserAsync(3);
        var c = f.Anonymous();
        for (var i = 0; i < 5; i++)
        {
            var bad = await c.PostAsJsonAsync("/api/auth/login", new { phone = u.Phone, pin = "9999" });
            Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        }
        // PIN မှန်ရင်တောင် လော့ခ်ကျနေ
        var locked = await c.PostAsJsonAsync("/api/auth/login", new { phone = u.Phone, pin = u.Pin });
        Assert.Equal((HttpStatusCode)429, locked.StatusCode);
    }

    [Fact]
    public async Task Correct_pin_resets_failed_counter()
    {
        var u = await f.CreateUserAsync(3);
        var c = f.Anonymous();
        for (var i = 0; i < 4; i++)
            await c.PostAsJsonAsync("/api/auth/login", new { phone = u.Phone, pin = "9999" });
        var ok = await c.PostAsJsonAsync("/api/auth/login", new { phone = u.Phone, pin = u.Pin });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        for (var i = 0; i < 4; i++)   // ထပ်မှားပေမယ့် ၅ ကြိမ်မပြည့်သေး
            await c.PostAsJsonAsync("/api/auth/login", new { phone = u.Phone, pin = "9999" });
        var ok2 = await c.PostAsJsonAsync("/api/auth/login", new { phone = u.Phone, pin = u.Pin });
        Assert.Equal(HttpStatusCode.OK, ok2.StatusCode);
    }

    [Fact]
    public async Task Bootstrap_with_wrong_setup_key_returns_401()
    {
        var res = await f.Anonymous().PostAsJsonAsync("/api/auth/bootstrap", new
        {
            tenantName = "X", ownerName = "X", phone = "09000000000", pin = "1234", setupKey = "wrong"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Bootstrap_when_users_exist_returns_409()
    {
        var res = await f.Anonymous().PostAsJsonAsync("/api/auth/bootstrap", new
        {
            tenantName = "X", ownerName = "X", phone = "09000000000", pin = "1234",
            setupKey = ApiFixture.SetupKey
        });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }
}
