using System.Net;
using Microsoft.EntityFrameworkCore;

namespace DocIntel.Api.Tests;

// UC-06 Restore an older version and UC-08 Retire or archive a document.
public class RestoreAndRetireTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private ContentClient Contributor => new(factory, TestUsers.Contributor);
    private ContentClient Contributor2 => new(factory, TestUsers.Contributor2);
    private ContentClient Reader => new(factory, TestUsers.Reader);
    private ContentClient Admin => new(factory, TestUsers.Admin);

    // A document at 3.0 with older published versions 1.0 and 2.0.
    private async Task<Guid> DocumentAt3_0()
    {
        var doc = (await Contributor.CheckInNewOk(content: "content A")).Id;
        await Admin.PublishLatestOk(doc); // 1.0 = A
        foreach (var content in new[] { "content B", "content C" })
        {
            await Contributor.CheckOutOk(doc);
            await Contributor.CheckInOk(doc, content);
            await Admin.PublishLatestOk(doc); // 2.0 = B, 3.0 = C
        }
        return doc;
    }

    private Task<List<string>> Snapshot(Guid doc) => factory.WithDb(async db =>
        (await db.Versions.Where(v => v.DocumentId == doc).OrderBy(v => v.Major).ThenBy(v => v.Minor).ToListAsync())
            .Select(v => $"{v.Id}|{v.Number}|{v.Status}|{v.BlobPath}|{v.Sha256}|{v.AuthorOid}|{v.CreatedAt:O}|{v.SourceVersionId}")
            .ToList());

    // ---- UC-06 Restore an older version ----

    [Fact]
    public async Task UC06_restoring_1_0_at_3_0_creates_draft_3_1_with_its_content_and_changes_nothing_else()
    {
        var doc = await DocumentAt3_0();
        var history = await Contributor.HistoryOk(doc);
        var v1 = history.Single(v => v.Number == "1.0");
        var before = await Snapshot(doc);

        var after = await Contributor2.RestoreOk(doc, v1.Id);

        var restored = after.LatestVersion!;
        Assert.Equal(("3.1", "Draft", "1.0"), (restored.Number, restored.Status, restored.SourceNumber));
        Assert.Equal("Second Contributor", restored.AuthorName);
        Assert.Equal("content A", await Contributor.OpenTextOk(doc, restored.Id));
        // History is never rewritten: every earlier version is exactly as it was.
        Assert.Equal(before, (await Snapshot(doc)).Take(before.Count));
        Assert.Equal(before.Count + 1, (await Snapshot(doc)).Count);
        Assert.Equal("3.0", after.PublishedVersion!.Number);
        Assert.Contains("version.restored", await factory.AuditActions(doc));
    }

    [Fact]
    public async Task UC06_a_restore_is_published_like_any_other_draft()
    {
        var doc = await DocumentAt3_0();
        var v1 = (await Contributor.HistoryOk(doc)).Single(v => v.Number == "1.0");
        await Contributor.RestoreOk(doc, v1.Id);

        var published = await Admin.PublishLatestOk(doc);

        Assert.Equal(("4.0", "3.1"), (published.PublishedVersion!.Number, published.PublishedVersion.SourceNumber));
        Assert.Equal("content A", await Reader.OpenTextOk(doc, published.PublishedVersion.Id));
    }

    [Fact]
    public async Task UC06_an_open_checkout_from_before_the_restore_becomes_out_of_date()
    {
        var doc = await DocumentAt3_0();
        await Contributor2.CheckOutOk(doc);
        var v2 = (await Contributor.HistoryOk(doc)).Single(v => v.Number == "2.0");
        await Contributor.RestoreOk(doc, v2.Id);

        var response = await Contributor2.CheckIn(doc);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UC06_readers_cannot_restore_and_the_latest_version_cannot_be_restored_onto_itself()
    {
        var doc = await DocumentAt3_0();
        var history = await Contributor.HistoryOk(doc);

        Assert.Equal(HttpStatusCode.Forbidden, (await Reader.Restore(doc, history.Single(v => v.Number == "1.0").Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Contributor.Restore(doc, history.Single(v => v.Number == "3.0").Id)).StatusCode);
    }

    // ---- UC-08 Retire or archive a document ----

    [Fact]
    public async Task UC08_retiring_hides_the_document_but_keeps_versions_and_audit_trail_readable()
    {
        var doc = await DocumentAt3_0();
        var before = await Snapshot(doc);

        var retired = await Admin.RetireOk(doc, "Superseded by the new handbook");

        Assert.True(retired.IsRetired);
        Assert.Equal("Test Admin", retired.RetiredByName);
        Assert.DoesNotContain(await Reader.ListOk(), d => d.Id == doc);
        Assert.DoesNotContain(await Contributor.ListOk(), d => d.Id == doc);
        Assert.Contains(await Reader.ListOk(retired: true), d => d.Id == doc);
        // Still viewable by those with access.
        var history = await Reader.HistoryOk(doc);
        Assert.Equal("content C", await Reader.OpenTextOk(doc, history.First().Id));
        Assert.True((await Reader.GetOk(doc)).IsRetired);
        // Nothing removed.
        Assert.Equal(before, await Snapshot(doc));
        var ev = await factory.WithDb(db => db.AuditEvents.SingleAsync(e => e.DocumentId == doc && e.Action == "document.retired"));
        Assert.Contains("Superseded by the new handbook", ev.Details);
    }

    [Fact]
    public async Task UC08_a_retired_document_cannot_be_changed_or_published()
    {
        var doc = await DocumentAt3_0();
        await Contributor.CheckOutOk(doc);
        await Admin.RetireOk(doc);

        Assert.Equal(HttpStatusCode.Conflict, (await Contributor2.CheckOut(doc)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Contributor.CheckIn(doc)).StatusCode);
        Assert.False((await Contributor.GetOk(doc)).CanEdit);
    }

    [Fact]
    public async Task UC08_an_administrator_can_reverse_the_retirement()
    {
        var doc = await DocumentAt3_0();
        await Admin.RetireOk(doc);

        var back = await Admin.UnretireOk(doc);

        Assert.False(back.IsRetired);
        Assert.Contains(await Reader.ListOk(), d => d.Id == doc);
        Assert.Equal("3.0", (await Contributor.CheckOutOk(doc)).MyCheckout!.BaseVersionNumber);
        var actions = await factory.AuditActions(doc);
        Assert.Equal(["document.retired", "document.unretired"], actions.Where(a => a.StartsWith("document.re") || a.StartsWith("document.un")));
    }

    [Fact]
    public async Task UC08_only_administrators_can_retire()
    {
        var doc = await DocumentAt3_0();

        Assert.Equal(HttpStatusCode.Forbidden, (await Contributor.Retire(doc)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Reader.Retire(doc)).StatusCode);
        Assert.False((await Reader.GetOk(doc)).IsRetired);
    }

    [Fact]
    public async Task UC08_retiring_twice_or_reversing_an_active_document_is_refused()
    {
        var doc = await DocumentAt3_0();

        Assert.Equal(HttpStatusCode.Conflict, (await Admin.Unretire(doc)).StatusCode);
        await Admin.RetireOk(doc);
        Assert.Equal(HttpStatusCode.Conflict, (await Admin.Retire(doc)).StatusCode);
    }
}
