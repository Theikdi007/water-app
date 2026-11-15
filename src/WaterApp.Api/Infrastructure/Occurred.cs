namespace WaterApp.Api.Infrastructure;

public static class Occurred
{
    // Client ပို့တဲ့ အချိန်ကို UTC ပြောင်း။ အနာဂတ် (၅ မိနစ်ကျော်) သို့မဟုတ် ရက် ၃၀ ကျော်ဟောင်းရင် error
    public static (DateTime At, string? Error) Resolve(DateTime? client)
    {
        var now = DateTime.UtcNow;
        if (client is null) return (now, null);

        var t = client.Value.Kind == DateTimeKind.Local
            ? client.Value.ToUniversalTime()
            : DateTime.SpecifyKind(client.Value, DateTimeKind.Utc);

        if (t > now.AddMinutes(5)) return (t, "occurredAt က အနာဂတ် ဖြစ်နေတယ်");
        if (t < now.AddDays(-30)) return (t, "occurredAt က ရက် ၃၀ ထက် ဟောင်းနေတယ်");
        return (t, null);
    }
}
