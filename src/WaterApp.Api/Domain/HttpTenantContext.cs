namespace WaterApp.Api.Domain;

// Token ထဲက tenant_id ကို ယူ (login မဝင်ရသေးရင် Guid.Empty = ဘာ data မှ မမြင်ရ)
public class HttpTenantContext(IHttpContextAccessor http) : ITenantContext
{
    public Guid TenantId =>
        Guid.TryParse(http.HttpContext?.User.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;
}
