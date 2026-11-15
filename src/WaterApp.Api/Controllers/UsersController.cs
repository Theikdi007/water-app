using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterApp.Api.Data;
using WaterApp.Api.Domain;

namespace WaterApp.Api.Controllers;

public record CreateUser(string Name, string Phone, string Pin, UserRole Role);

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Owner")]
public class UsersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(await db.AppUsers.OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Phone, role = x.Role.ToString(), x.IsActive })
            .ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(CreateUser r)
    {
        if (!AuthController.ValidPin(r.Pin)) return BadRequest("PIN က ဂဏန်း ၄ လုံးမှ ၆ လုံးဖြစ်ရမယ်");
        if (await db.AppUsers.IgnoreQueryFilters().AnyAsync(x => x.Phone == r.Phone))
            return Conflict("ဒီဖုန်းနံပါတ် သုံးပြီးသားပါ");

        var u = new AppUser { Id = Guid.NewGuid(), Name = r.Name, Phone = r.Phone, Role = r.Role };
        u.PinHash = AuthController.HashPin(u, r.Pin);
        db.AppUsers.Add(u);
        await db.SaveChangesAsync();
        return Ok(new { u.Id, u.Name, role = u.Role.ToString() });
    }
}
