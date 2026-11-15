using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using WaterApp.Api.Data;
using WaterApp.Api.Domain;

namespace WaterApp.Api.Controllers;

public record LoginRequest(string Phone, string Pin);
public record BootstrapRequest(string TenantName, string OwnerName, string Phone, string Pin);

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, IConfiguration cfg) : ControllerBase
{
    static readonly PasswordHasher<AppUser> Hasher = new();
    static readonly Guid DevTenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static bool ValidPin(string pin) => Regex.IsMatch(pin ?? "", @"^\d{4,6}$");
    public static string HashPin(AppUser u, string pin) => Hasher.HashPassword(u, pin);

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest r)
    {
        var u = await db.AppUsers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Phone == r.Phone && x.IsActive);
        if (u is null || Hasher.VerifyHashedPassword(u, u.PinHash, r.Pin ?? "") == PasswordVerificationResult.Failed)
            return Unauthorized("ဖုန်းနံပါတ် ဒါမှမဟုတ် PIN မှားနေပါတယ်");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
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

    // ပထမဆုံး Owner ကို ဖန်တီးဖို့ (user တစ်ယောက်မှ မရှိသေးမှသာ အလုပ်လုပ်)
    [AllowAnonymous]
    [HttpPost("bootstrap")]
    public async Task<IActionResult> Bootstrap(BootstrapRequest r)
    {
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
