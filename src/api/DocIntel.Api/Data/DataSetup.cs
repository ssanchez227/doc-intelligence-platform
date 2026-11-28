using DocIntel.Api.Storage;
using Microsoft.EntityFrameworkCore;

namespace DocIntel.Api.Data;

public static class DataSetup
{
    // Database:Provider is "SqlServer" (Azure SQL, migrations) or "Sqlite" (tests and
    // local runs, created from the model). ConnectionStrings:DocIntel points at it.
    // With no provider configured the API still starts (health answers) but every
    // data endpoint fails: an empty in-memory database stands in.
    public static IServiceCollection AddDocIntelData(this IServiceCollection services, IConfiguration config)
    {
        var provider = config["Database:Provider"];
        var connection = provider is null ? "Data Source=:memory:" : config.GetConnectionString("DocIntel")
            ?? throw new InvalidOperationException("ConnectionStrings:DocIntel is not set.");

        services.AddDbContext<DocIntelDb>(options =>
        {
            if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connection, sql => sql.EnableRetryOnFailure());
            }
            else
            {
                options.UseSqlite(connection);
            }
        });

        var storage = config.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
        services.AddSingleton(storage);
        services.AddSingleton<IFileStore>(storage.Provider.Equals("Blob", StringComparison.OrdinalIgnoreCase)
            ? new BlobFileStore(storage)
            : new LocalFileStore(storage));

        return services;
    }

    // Azure SQL: apply migrations when Database:MigrateOnStartup is true (the API's
    // identity holds db_ddladmin; revisit before M8). SQLite: create from the model.
    public static async Task PrepareDatabaseAsync(this WebApplication app)
    {
        if (app.Configuration["Database:Provider"] is null)
        {
            app.Logger.LogWarning("Database:Provider is not set; data endpoints will fail.");
            return;
        }

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocIntelDb>();

        if (db.Database.IsSqlite())
        {
            var source = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
            var inMemory = source == ":memory:" || source.StartsWith("file:", StringComparison.Ordinal);
            if (!inMemory && Path.GetDirectoryName(Path.GetFullPath(source)) is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
            }
            await db.Database.EnsureCreatedAsync();
        }
        else if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            await db.Database.MigrateAsync();
        }
    }
}
