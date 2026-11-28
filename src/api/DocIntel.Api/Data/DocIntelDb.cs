using DocIntel.Api.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DocIntel.Api.Data;

// One database for the API. Azure SQL in Azure (with migrations); SQLite for tests
// and local runs (created from the model). Keep the model portable between both:
// no provider-specific column types, and filters written in plain SQL.
public sealed class DocIntelDb(DbContextOptions<DocIntelDb> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentGroup> DocumentGroups => Set<DocumentGroup>();
    public DbSet<DocumentVersion> Versions => Set<DocumentVersion>();
    public DbSet<Checkout> Checkouts => Set<Checkout>();
    public DbSet<ReviewerAssignment> ReviewerAssignments => Set<ReviewerAssignment>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void ConfigureConventions(ModelConfigurationBuilder conventions)
    {
        // SQLite cannot order or compare DateTimeOffset stored as text; store it as a
        // sortable number there. Azure SQL keeps datetimeoffset.
        if (Database.IsSqlite())
        {
            conventions.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Category>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(100);
            e.HasIndex(c => c.Name).IsUnique();
            e.HasMany(c => c.DefaultGroups).WithOne().HasForeignKey(g => g.CategoryId);
        });

        model.Entity<CategoryDefaultGroup>(e =>
        {
            e.HasKey(g => new { g.CategoryId, g.GroupKey });
            e.Property(g => g.GroupKey).HasMaxLength(64);
        });

        model.Entity<Document>(e =>
        {
            e.Property(d => d.Title).HasMaxLength(200);
            e.Property(d => d.Description).HasMaxLength(2000);
            e.Property(d => d.OwnerOid).HasMaxLength(64);
            e.Property(d => d.OwnerName).HasMaxLength(200);
            e.Property(d => d.RetiredByName).HasMaxLength(200);
            e.Property(d => d.ConcurrencyStamp).IsConcurrencyToken();
            e.HasOne(d => d.Category).WithMany().HasForeignKey(d => d.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(d => d.Versions).WithOne(v => v.Document).HasForeignKey(v => v.DocumentId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(d => d.Groups).WithOne().HasForeignKey(g => g.DocumentId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(d => d.Checkouts).WithOne().HasForeignKey(c => c.DocumentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DocumentVersion>().WithMany().HasForeignKey(d => d.PublishedVersionId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<DocumentGroup>(e =>
        {
            e.HasKey(g => new { g.DocumentId, g.GroupKey });
            e.Property(g => g.GroupKey).HasMaxLength(64);
        });

        model.Entity<DocumentVersion>(e =>
        {
            // One row per number: two simultaneous check-ins cannot both take 1.1.
            e.HasIndex(v => new { v.DocumentId, v.Major, v.Minor }).IsUnique();
            e.Ignore(v => v.Number);
            e.Property(v => v.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(v => v.PdfStatus).HasConversion<string>().HasMaxLength(16);
            e.Property(v => v.FileName).HasMaxLength(255);
            e.Property(v => v.ContentType).HasMaxLength(127);
            e.Property(v => v.Sha256).HasMaxLength(64);
            e.Property(v => v.BlobPath).HasMaxLength(400);
            e.Property(v => v.PdfBlobPath).HasMaxLength(400);
            e.Property(v => v.PdfError).HasMaxLength(1000);
            e.Property(v => v.AuthorOid).HasMaxLength(64);
            e.Property(v => v.AuthorName).HasMaxLength(200);
            e.Property(v => v.Comment).HasMaxLength(1000);
            e.HasOne<DocumentVersion>().WithMany().HasForeignKey(v => v.SourceVersionId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<Checkout>(e =>
        {
            e.Property(c => c.UserOid).HasMaxLength(64);
            e.Property(c => c.UserName).HasMaxLength(200);
            e.Property(c => c.CloseReason).HasConversion<string>().HasMaxLength(16);
            // At most one open checkout per user per document.
            e.HasIndex(c => new { c.DocumentId, c.UserOid }).IsUnique().HasFilter("ClosedAt IS NULL");
            e.HasOne<DocumentVersion>().WithMany().HasForeignKey(c => c.BaseVersionId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ReviewerAssignment>(e =>
        {
            e.Property(r => r.ReviewerOid).HasMaxLength(64);
            e.Property(r => r.ReviewerName).HasMaxLength(200);
            e.HasIndex(r => new { r.VersionId, r.Order }).IsUnique();
            e.HasOne<DocumentVersion>().WithMany().HasForeignKey(r => r.VersionId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<AuditEvent>(e =>
        {
            e.Property(a => a.ActorOid).HasMaxLength(64);
            e.Property(a => a.ActorName).HasMaxLength(200);
            e.Property(a => a.Action).HasMaxLength(64);
            e.HasIndex(a => new { a.DocumentId, a.At });
        });

        Seed.Categories(model);
    }
}
