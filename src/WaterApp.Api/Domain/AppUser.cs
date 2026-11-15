namespace WaterApp.Api.Domain;

public enum UserRole { Owner = 1, Cashier = 2, Driver = 3 }

public class AppUser : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string PinHash { get; set; } = "";
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
}
