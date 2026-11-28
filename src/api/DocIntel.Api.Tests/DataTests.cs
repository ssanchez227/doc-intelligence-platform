using System.Net;
using System.Net.Http.Json;
using System.Text;
using DocIntel.Api.Auth;
using DocIntel.Api.Content;
using DocIntel.Api.Data;
using DocIntel.Api.Storage;
using Microsoft.EntityFrameworkCore;

namespace DocIntel.Api.Tests;

// The data layer: schema, seed, the database rules that back optimistic
// concurrency and immutability, audit events and file storage.
public class DataTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Categories_are_seeded_with_default_groups()
    {
        var groups = await factory.WithDb(db => db.Categories
            .Where(c => c.Name == "Finance").SelectMany(c => c.DefaultGroups.Select(g => g.GroupKey)).ToListAsync());

        Assert.Equal([GroupKeys.Finance], groups);
    }

    [Fact]
    public async Task Categories_endpoint_lists_them_for_a_signed_in_user()
    {
        var categories = await factory.ClientFor(TestUsers.Reader).GetFromJsonAsync<CategoryDto[]>("/api/categories");

        Assert.Equal(["Finance", "General", "HR", "Policies"], categories!.Select(c => c.Name));
    }

    [Fact]
    public async Task Categories_endpoint_refuses_anonymous_callers()
    {
        var response = await factory.CreateClient().GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_version_number_can_exist_only_once_per_document()
    {
        var doc = await NewDocumentWithVersion(0, 1);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDb(async db =>
        {
            db.Versions.Add(NewVersion(doc, 0, 1));
            return await db.SaveChangesAsync();
        }));
        Assert.NotNull(ex);
    }

    [Fact]
    public async Task A_user_has_at_most_one_open_checkout_per_document_but_closed_ones_stay()
    {
        var doc = await NewDocumentWithVersion(0, 1);
        var baseVersion = await factory.WithDb(db => db.Versions.Where(v => v.DocumentId == doc).Select(v => v.Id).SingleAsync());
        Checkout Open(DateTimeOffset? closed = null) => new()
        {
            Id = Guid.NewGuid(), DocumentId = doc, UserOid = "u1", UserName = "U1",
            TakenAt = DateTimeOffset.UtcNow, BaseVersionId = baseVersion, ClosedAt = closed,
            CloseReason = closed is null ? null : CheckoutCloseReason.Cancelled,
        };

        await factory.WithDb(async db =>
        {
            db.Checkouts.AddRange(Open(DateTimeOffset.UtcNow), Open(DateTimeOffset.UtcNow), Open());
            return await db.SaveChangesAsync();
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDb(async db =>
        {
            db.Checkouts.Add(Open());
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task A_stale_document_write_is_refused()
    {
        var doc = await NewDocumentWithVersion(0, 1);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => factory.WithDb(async db =>
        {
            var first = await db.Documents.SingleAsync(d => d.Id == doc);
            // Someone else writes the document after we read it.
            await factory.WithDb(async other =>
            {
                var d = await other.Documents.SingleAsync(x => x.Id == doc);
                d.Title = "Changed elsewhere";
                d.ConcurrencyStamp = Guid.NewGuid();
                return await other.SaveChangesAsync();
            });
            first.Title = "Stale write";
            first.ConcurrencyStamp = Guid.NewGuid();
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task Audit_events_are_saved_with_the_change()
    {
        var actor = new CurrentUser("oid-a", "Actor", new HashSet<string>());
        var id = Guid.NewGuid();

        await factory.WithDb(async db =>
        {
            db.Record(actor, Audit.Actions.DocumentRetired, documentId: id, details: new { reason = "test" });
            return await db.SaveChangesAsync();
        });

        var ev = await factory.WithDb(db => db.AuditEvents.SingleAsync(e => e.DocumentId == id));
        Assert.Equal(("oid-a", "Actor", "document.retired", """{"reason":"test"}"""), (ev.ActorOid, ev.ActorName, ev.Action, ev.Details));
    }

    [Fact]
    public async Task Local_file_store_never_overwrites_and_stays_in_its_folder()
    {
        var store = new LocalFileStore(new StorageOptions { LocalPath = Path.Combine(Path.GetTempPath(), "docintel-tests", Guid.NewGuid().ToString("N")) });
        var ct = CancellationToken.None;

        await store.WriteAsync("a/b.txt", new MemoryStream("one"u8.ToArray()), "text/plain", ct);
        await using (var read = await store.OpenReadAsync("a/b.txt", ct))
        {
            Assert.Equal("one", await new StreamReader(read, Encoding.UTF8).ReadToEndAsync(ct));
        }

        await Assert.ThrowsAsync<IOException>(() => store.WriteAsync("a/b.txt", new MemoryStream("two"u8.ToArray()), "text/plain", ct));
        await Assert.ThrowsAsync<ArgumentException>(() => store.WriteAsync("../escape.txt", new MemoryStream(), "text/plain", ct));
    }

    private async Task<Guid> NewDocumentWithVersion(int major, int minor)
    {
        var doc = new Document
        {
            Id = Guid.NewGuid(), Title = "Doc", CategoryId = 1, OwnerOid = "o", OwnerName = "Owner",
            CreatedAt = DateTimeOffset.UtcNow, ConcurrencyStamp = Guid.NewGuid(),
        };
        await factory.WithDb(async db =>
        {
            db.Documents.Add(doc);
            db.Versions.Add(NewVersion(doc.Id, major, minor));
            return await db.SaveChangesAsync();
        });
        return doc.Id;
    }

    private static DocumentVersion NewVersion(Guid documentId, int major, int minor) => new()
    {
        Id = Guid.NewGuid(), DocumentId = documentId, Major = major, Minor = minor, Status = VersionStatus.Draft,
        FileName = "a.txt", ContentType = "text/plain", SizeBytes = 1, Sha256 = new string('0', 64),
        BlobPath = $"{documentId}/{Guid.NewGuid()}", AuthorOid = "o", AuthorName = "Owner", CreatedAt = DateTimeOffset.UtcNow,
    };

    private sealed record CategoryDto(int Id, string Name);
}
