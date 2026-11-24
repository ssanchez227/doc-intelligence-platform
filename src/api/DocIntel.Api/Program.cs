using System.Security.Claims;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// Telemetry goes to Application Insights from the first request whenever the
// connection string is present (Container Apps sets it; local runs skip it).
if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
}

// Entra ID access tokens. TenantId and Audience come from configuration
// (Auth__TenantId, Auth__Audience); the app registrations are created in M0 step 6.
var auth = builder.Configuration.GetSection("Auth");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://login.microsoftonline.com/{auth["TenantId"]}/v2.0";
        options.Audience = auth["Audience"];
        options.MapInboundClaims = false;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .AllowAnonymous();

// Proves sign-in end to end: only a valid Entra token gets past this.
app.MapGet("/api/me", (ClaimsPrincipal user) => Results.Ok(new
   {
       name = user.FindFirstValue("name") ?? user.FindFirstValue("preferred_username"),
       objectId = user.FindFirstValue("oid"),
       groups = user.FindAll("groups").Select(c => c.Value).ToArray(),
   }))
   .RequireAuthorization();

app.Run();

public partial class Program;
