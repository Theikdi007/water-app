using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace WaterApp.Api.Infrastructure;

// OpenAPI document ထဲကို "Bearer token" security scheme ထည့်ပေး၊ Swagger UI မှာ Authorize ခလုတ် ပေါ်လာမယ်
internal sealed class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider schemes)
    : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document,
        OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var all = await schemes.GetAllSchemesAsync();
        if (!all.Any(s => s.Name == "Bearer")) return;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                In = ParameterLocation.Header,
                BearerFormat = "JWT"
            }
        };

        foreach (var operation in document.Paths.Values.SelectMany(p => p.Operations))
        {
            operation.Value.Security ??= [];
            operation.Value.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        }
    }
}
