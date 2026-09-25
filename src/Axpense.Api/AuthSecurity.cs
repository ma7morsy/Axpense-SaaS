using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Axpense.Api;

public sealed class TenantAuthorizationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/auth") || context.Request.Path.StartsWithSegments("/health") || context.Request.Path.StartsWithSegments("/ready"))
        {
            await next(context); return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var organizationId = context.User.FindFirstValue("organization_id");
        if (!Guid.TryParse(organizationId, out var tenantId))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (context.Request.Query.TryGetValue("organizationId", out var queryValue) &&
            Guid.TryParse(queryValue.FirstOrDefault(), out var queryTenant) && queryTenant != tenantId)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden; return;
        }

        if (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method))
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("organizationId", out var property) &&
                        Guid.TryParse(property.GetString(), out var bodyTenant) && bodyTenant != tenantId)
                    { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
                }
                catch (JsonException) { }
            }
        }
        await next(context);
    }
}

public static class JwtExtensions
{
    public static void AddAxpenseAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = configuration["Jwt:Issuer"],
                ValidateAudience = true, ValidAudience = configuration["Jwt:Audience"],
                ValidateIssuerSigningKey = true, IssuerSigningKey = signingKey,
                ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1)
            };
        });
        services.AddAuthorization();
    }
}
