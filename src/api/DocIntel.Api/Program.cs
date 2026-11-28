using System.Security.Claims;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using DocIntel.Api.Auth;
using DocIntel.Api.Content;
using DocIntel.Api.Content.Pdf;
using DocIntel.Api.Data;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Telemetry goes to Application Insights from the first request whenever the
// connection string is present (Container Apps sets it; local runs skip it).
if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
}

// Entra ID access tokens. TenantId and Audience come from configuration
// (Auth__TenantId, Auth__Audience); the app registrations live in infra/identity.
// Audience is the API's client ID, which v2 access tokens carry as "aud".
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

// The web app is served from its own origin, so the browser needs CORS to call the
// API. Allowed origins come from configuration (Cors__AllowedOrigins__0, ...).
builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>().Configure<IConfiguration>((options, config) =>
{
    var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(origins)
        .WithHeaders("Authorization", "Content-Type")
        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE"));
});

// Data and files (M1): see Data/DataSetup.cs for the providers per environment.
builder.Services.AddDocIntelData(builder.Configuration);

// Token group IDs -> group keys (configuration section "Groups"), and the
// signed-in user as the endpoints see it. Resolve CurrentUser only behind
// RequireAuthorization.
builder.Services.AddSingleton(builder.Configuration.GetSection("Groups").Get<GroupMap>() ?? new GroupMap());
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped(sp => CurrentUser.From(
    sp.GetRequiredService<IHttpContextAccessor>().HttpContext!.User, sp.GetRequiredService<GroupMap>()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<DocumentService>();
builder.Services.AddSingleton(new ContentFeatures(builder.Configuration.GetValue<bool>("Features:DevPublish")));
builder.Services.AddSingleton<PdfQueue>();
builder.Services.AddSingleton<PdfConverter>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PdfConverter>());

var app = builder.Build();

await app.PrepareDatabaseAsync();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .AllowAnonymous();

// Proves sign-in end to end: only a valid Entra token gets past this.
app.MapGet("/api/me", (ClaimsPrincipal user, CurrentUser current) => Results.Ok(new
   {
       name = user.FindFirstValue("name") ?? user.FindFirstValue("preferred_username"),
       objectId = user.FindFirstValue("oid"),
       groups = user.FindAll("groups").Select(c => c.Value).ToArray(),
       // What the web app needs to decide which actions to offer; the API checks again.
       groupKeys = current.GroupKeys.Order().ToArray(),
   }))
   .RequireAuthorization();

app.MapGet("/api/categories", async (DocIntelDb db, CancellationToken ct) =>
       await db.Categories.OrderBy(c => c.Name).Select(c => new { c.Id, c.Name }).ToListAsync(ct))
   .RequireAuthorization();

app.MapDocumentEndpoints();

app.Run();

public partial class Program;
