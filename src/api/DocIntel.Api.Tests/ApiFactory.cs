using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using DocIntel.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocIntel.Api.Tests;

// The API with Entra replaced by a header-based test scheme, its own database and
// its own file folder. The database is SQLite in memory unless the environment
// variable DOCINTEL_TEST_SQLSERVER holds a SQL Server connection string (CI runs
// the suite both ways); then each factory gets a fresh database built by the
// migrations and dropped afterwards.
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string WebOrigin = "https://web.example";

    private static readonly string? SqlServer = Environment.GetEnvironmentVariable("DOCINTEL_TEST_SQLSERVER");

    private readonly string _filesPath = Path.Combine(Path.GetTempPath(), "docintel-tests", Guid.NewGuid().ToString("N"));
    private readonly string _connectionString;
    private readonly SqliteConnection? _keepAlive;

    public ApiFactory()
    {
        if (SqlServer is { Length: > 0 })
        {
            _connectionString = new SqlConnectionStringBuilder(SqlServer)
            {
                InitialCatalog = $"docintel_test_{Guid.NewGuid():N}",
            }.ConnectionString;
        }
        else
        {
            // A named shared in-memory database lives while one connection is open.
            _connectionString = $"Data Source=file:docintel-{Guid.NewGuid():N}?mode=memory&cache=shared";
            _keepAlive = new SqliteConnection(_connectionString);
            _keepAlive.Open();
        }
    }

    public bool UsesSqlServer => _keepAlive is null;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .UseSetting("Cors:AllowedOrigins:0", WebOrigin)
            .UseSetting("Database:Provider", UsesSqlServer ? "SqlServer" : "Sqlite")
            .UseSetting("Database:MigrateOnStartup", "true")
            .UseSetting("ConnectionStrings:DocIntel", _connectionString)
            .UseSetting("Storage:Provider", "Local")
            .UseSetting("Storage:LocalPath", _filesPath);

        foreach (var key in TestUsers.AllGroupKeys)
        {
            builder.UseSetting($"Groups:{key}", TestUsers.GroupId(key));
        }

        builder.ConfigureTestServices(services =>
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { }));
    }

    public HttpClient ClientFor(TestUser user)
    {
        var client = CreateClient();
        user.Apply(client.DefaultRequestHeaders);
        return client;
    }

    // Direct database access for assertions and test setup.
    public async Task<T> WithDb<T>(Func<DocIntelDb, Task<T>> work)
    {
        using var scope = Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<DocIntelDb>());
    }

    // Cleans up after the host is gone, without touching its (disposed) services.
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        _keepAlive?.Dispose();
        if (UsesSqlServer)
        {
            SqlConnection.ClearAllPools();
            using var db = new DocIntelDb(new DbContextOptionsBuilder<DocIntelDb>().UseSqlServer(_connectionString).Options);
            db.Database.EnsureDeleted();
        }
        if (Directory.Exists(_filesPath)) Directory.Delete(_filesPath, recursive: true);
    }
}

public sealed record TestUser(string Oid, string Name, params string[] GroupKeys)
{
    public void Apply(HttpRequestHeaders headers)
    {
        headers.Add(TestAuthHandler.NameHeader, Name);
        headers.Add(TestAuthHandler.OidHeader, Oid);
        headers.Add(TestAuthHandler.GroupsHeader, string.Join(',', GroupKeys.Select(TestUsers.GroupId)));
    }
}

// Stand-ins for the test users in infra/identity.
public static class TestUsers
{
    public static readonly string[] AllGroupKeys = ["readers", "contributors", "reviewers", "administrators", "finance", "hr"];

    public static string GroupId(string key) => $"gid-{key}";

    public static readonly TestUser Reader = new("oid-reader", "Test Reader", "readers");
    public static readonly TestUser Contributor = new("oid-contributor", "Test Contributor", "contributors");
    public static readonly TestUser Contributor2 = new("oid-contributor-2", "Second Contributor", "contributors");
    public static readonly TestUser Admin = new("oid-admin", "Test Admin", "administrators");
    public static readonly TestUser Outsider = new("oid-outsider", "Test Outsider");
}

// Replaces Entra token validation: the caller's name, oid and group IDs come from
// headers, so tests check the endpoint rules without a real tenant.
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string NameHeader = "X-Test-User";
    public const string OidHeader = "X-Test-Oid";
    public const string GroupsHeader = "X-Test-Groups";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(NameHeader, out var name))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new("name", name!),
            new("oid", Request.Headers[OidHeader].FirstOrDefault() ?? Guid.Empty.ToString()),
        };
        var groups = Request.Headers[GroupsHeader].FirstOrDefault() ?? "";
        claims.AddRange(groups.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(g => new Claim("groups", g)));

        var identity = new ClaimsIdentity(claims, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
