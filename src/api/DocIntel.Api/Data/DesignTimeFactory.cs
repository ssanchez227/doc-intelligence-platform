using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DocIntel.Api.Data;

// Migrations are for Azure SQL only (SQLite is created from the model), so
// `dotnet ef migrations add` always builds the model for SQL Server. The
// connection string is never opened.
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<DocIntelDb>
{
    public DocIntelDb CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<DocIntelDb>().UseSqlServer("Server=design-time;Database=DocIntel").Options);
}
