using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WaterApp.Api.Data;
using WaterApp.Api.Domain;

namespace WaterApp.Api.Controllers;

public record LoginRequest(string Phone, string Pin);
public record BootstrapRequest(string TenantName, string OwnerName, string Phone, string Pin, string SetupKey);

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, IConfiguration cfg) : ControllerBase
{
    static readonly PasswordHasher<AppUser> Hasher = new();
    static readonly Guid DevTenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    const int MaxFails = 5;
    static readonly TimeSpan LockFor = TimeSpan.FromMinutes(15);
    const string BadLogin = "ဖုန်းနံပါတ် ဒါမှမဟုတ် PIN မှားနေပါတယ်";

    public static bool ValidPin(string pin) => Regex.IsMatch(pin ?? "", @"^\d{4,6}$");
    public static string HashPin(AppUser u, string pin) => Hasher.HashPassword(u, pin);

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest r)
    {
        var u = await db.AppUsers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Phone == r.Phone && x.IsActive);
        if (u is null) return Unauthorized(BadLogin);

        if (u.LockoutUntil is { } until && until > DateTime.UtcNow)
            return StatusCode(429, "PIN အကြိမ်များစွာ မှားထားလို့ ခဏပိတ်ထားပါတယ်၊ မိနစ် ၁၅ ကြာမှ ပြန်စမ်းပါ");

        if (Hasher.VerifyHashedPassword(u, u.PinHash, r.Pin ?? "") == PasswordVerificationResult.Failed)
        {
            u.FailedLogins++;
            if (u.FailedLogins >= MaxFails)
            {
                u.LockoutUntil = DateTime.UtcNow.Add(LockFor);
                u.FailedLogins = 0;
            }
            await db.SaveChangesAsync();
            return Unauthorized(BadLogin);
        }

        if (u.FailedLogins != 0 || u.LockoutUntil is not null)
        {
            u.FailedLogins = 0;
            u.LockoutUntil = null;
            await db.SaveChangesAsync();
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            cfg["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key မသတ်မှတ်ရသေးပါ")));
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                ["sub"] = u.Id.ToString(),
                ["tenant_id"] = u.TenantId.ToString(),
                ["role"] = u.Role.ToString(),
                ["name"] = u.Name
            },
            Issuer = cfg["Jwt:Issuer"], Audience = cfg["Jwt:Audience"],
            Expires = DateTime.UtcNow.AddHours(12),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        });
        return Ok(new { token, name = u.Name, role = u.Role.ToString() });
    }

    // ပထမဆုံး Owner ကို ဖန်တီးဖို့ (user မရှိသေးမှ + setup key မှန်မှ)
    [AllowAnonymous]
    [HttpPost("bootstrap")]
    public async Task<IActionResult> Bootstrap(BootstrapRequest r)
    {
        var setup = cfg["Setup:Key"];
        if (string.IsNullOrEmpty(setup)) return NotFound();   // config မထားရင် ပိတ်ထား
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(r.SetupKey ?? ""), Encoding.UTF8.GetBytes(setup)))
            return Unauthorized("setup key မှားနေတယ်");

        if (await db.AppUsers.IgnoreQueryFilters().AnyAsync())
            return Conflict("User ရှိပြီးသားပါ၊ bootstrap ထပ်မရပါ");
        if (!ValidPin(r.Pin)) return BadRequest("PIN က ဂဏန်း ၄ လုံးမှ ၆ လုံးဖြစ်ရမယ်");

        if (!await db.Tenants.AnyAsync(x => x.Id == DevTenant))
            db.Tenants.Add(new Tenant { Id = DevTenant, Name = r.TenantName });

        var u = new AppUser
        {
            Id = Guid.NewGuid(), TenantId = DevTenant, Name = r.OwnerName,
            Phone = r.Phone, Role = UserRole.Owner
        };
        u.PinHash = HashPin(u, r.Pin);
        db.AppUsers.Add(u);
        await db.SaveChangesAsync();
        return Ok(new { u.Id, u.Name, role = u.Role.ToString() });
    }
}
