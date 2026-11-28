using System.Net;
using DocIntel.Api.Content;
using Microsoft.EntityFrameworkCore;

namespace DocIntel.Api.Tests;

// UC-05 (history and opening older versions), the library list, and the publish
// method that M5's final approval will call.
public class HistoryAndPublishTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private ContentClient Contributor => new(factory, TestUsers.Contributor);
    private ContentClient Reader => new(factory, TestUsers.Reader);
    private ContentClient Admin => new(factory, TestUsers.Admin);
    private ContentClient Outsider => new(factory, TestUsers.Outsider);

    // 0.1 -> 1.0 published -> 1.1 draft -> 2.0 published (1.0 archived) -> 2.1 draft
    private async Task<Guid> DocumentWithHistory()
    {
        var doc = (await Contributor.CheckInNewOk(content: "content of 0.1")).Id;
        await Admin.PublishLatestOk(doc);
        await Contributor.CheckOutOk(doc);
        await Contributor.CheckInOk(doc, "content of 1.1");
        await Admin.PublishLatestOk(doc);
        await Contributor.CheckOutOk(doc);
        await Contributor.CheckInOk(doc, "content of 2.1");
        return doc;
    }

    // ---- Publishing ----

    [Fact]
    public async Task Publishing_the_latest_draft_creates_the_next_major_and_keeps_the_draft_unchanged()
    {
        var doc = (await Contributor.CheckInNewOk(content: "draft text")).Id;
        await Contributor.CheckOutOk(doc);
        await Contributor.CheckInOk(doc, "second draft");

        var after = await Admin.PublishLatestOk(doc);

        Assert.Equal("1.0", after.PublishedVersion!.Number);
        Assert.Equal("Published", after.PublishedVersion.Status);
        Assert.Equal("0.2", after.PublishedVersion.SourceNumber);
        Assert.Equal(["0.1", "0.2", "1.0"], await factory.VersionNumbers(doc));
        var (draft, published) = await factory.WithDb(async db =>
        {
            var vs = await db.Versions.Where(v => v.DocumentId == doc).ToListAsync();
            return (vs.Single(v => v.Minor == 2), vs.Single(v => v.Major == 1));
        });
        Assert.Equal(VersionStatus.Draft, draft.Status);
        Assert.Equal(draft.BlobPath, published.BlobPath);
        Assert.Equal(draft.Sha256, published.Sha256);
        Assert.Equal("second draft", await Reader.OpenTextOk(doc, published.Id));
        Assert.Contains("version.published", await factory.AuditActions(doc));
    }

    [Fact]
    public async Task Publishing_again_takes_the_next_major_and_archives_the_previous_published_version()
    {
        var doc = await DocumentWithHistory();

        var history = await Contributor.HistoryOk(doc);

        Assert.Equal(["2.1", "2.0", "1.1", "1.0", "0.1"], history.Select(v => v.Number));
        Assert.Equal(["Draft", "Published", "Draft", "Archived", "Draft"], history.Select(v => v.Status));
        Assert.Equal("2.0", (await Reader.GetOk(doc)).PublishedVersion!.Number);
    }

    [Fact]
    public async Task Only_the_latest_draft_can_be_published()
    {
        var doc = await DocumentWithHistory();
        var history = await Contributor.HistoryOk(doc);

        Assert.Equal(HttpStatusCode.Conflict, (await Admin.Publish(doc, history.Single(v => v.Number == "1.1").Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Admin.Publish(doc, history.Single(v => v.Number == "2.0").Id)).StatusCode);
    }

    [Fact]
    public async Task Only_administrators_can_use_the_dev_publish_endpoint()
    {
        var doc = await Contributor.CheckInNewOk();

        Assert.Equal(HttpStatusCode.Forbidden, (await Contributor.Publish(doc.Id, doc.LatestVersion!.Id)).StatusCode);
        Assert.False(doc.CanPublish);
        Assert.True((await Admin.GetOk(doc.Id)).CanPublish);
        await Admin.PublishLatestOk(doc.Id);
        Assert.False((await Admin.GetOk(doc.Id)).CanPublish); // nothing left to publish
    }

    // ---- UC-05 View version history and open an older version ----

    [Fact]
    public async Task UC05_a_reader_sees_every_version_they_may_open_with_number_author_date_and_status()
    {
        var doc = await DocumentWithHistory();

        var history = await Reader.HistoryOk(doc);

        // Drafts are not for readers; published and archived versions are.
        Assert.Equal(["2.0", "1.0"], history.Select(v => v.Number));
        Assert.Equal(["Published", "Archived"], history.Select(v => v.Status));
        Assert.All(history, v =>
        {
            Assert.Equal("Test Contributor", v.AuthorName);
            Assert.True(v.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-5));
        });
    }

    [Fact]
    public async Task UC05_a_reader_can_open_an_older_version_and_it_is_audited()
    {
        var doc = await DocumentWithHistory();
        var archived = (await Reader.HistoryOk(doc)).Single(v => v.Number == "1.0");

        var text = await Reader.OpenTextOk(doc, archived.Id);

        Assert.Equal("content of 0.1", text);
        var opened = await factory.WithDb(db => db.AuditEvents
            .Where(e => e.DocumentId == doc && e.Action == "version.opened").Select(e => new { e.ActorName, e.VersionId }).SingleAsync());
        Assert.Equal(("Test Reader", archived.Id), (opened.ActorName, opened.VersionId!.Value));
    }

    [Fact]
    public async Task UC05_a_version_the_reader_may_not_open_reveals_no_content()
    {
        var doc = await DocumentWithHistory();
        var draft = (await Contributor.HistoryOk(doc)).Single(v => v.Number == "2.1");

        var response = await Reader.Open(doc, draft.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("content of 2.1", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UC05_someone_without_access_gets_no_history_and_no_content()
    {
        var doc = await DocumentWithHistory();
        var published = (await Reader.HistoryOk(doc)).First();

        Assert.Equal(HttpStatusCode.NotFound, (await Outsider.History(doc)).StatusCode);
        var open = await Outsider.Open(doc, published.Id);
        Assert.Equal(HttpStatusCode.NotFound, open.StatusCode);
        Assert.DoesNotContain("content", await open.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UC05_a_version_of_another_document_cannot_be_opened_through_this_one()
    {
        var mine = await DocumentWithHistory();
        var hr = await Contributor.CheckInNewOk(categoryId: 4, content: "hr secret");

        var response = await Reader.Open(mine, hr.LatestVersion!.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Opened_files_are_downloads_that_the_browser_must_not_sniff()
    {
        var doc = await DocumentWithHistory();
        var published = (await Reader.HistoryOk(doc)).First();

        var response = await Reader.Open(doc, published.Id);

        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("text/markdown", response.Content.Headers.ContentType!.MediaType);
    }

    // ---- Library ----

    [Fact]
    public async Task The_library_lists_readable_documents_with_what_each_caller_may_see()
    {
        var doc = await DocumentWithHistory();
        var hr = await Contributor.CheckInNewOk(title: "HR only", categoryId: 4);

        var forReader = await Reader.ListOk();
        var forContributor = await Contributor.ListOk();

        var r = forReader.Single(d => d.Id == doc);
        Assert.Equal(("2.0", "2.0", "Published"), (r.PublishedNumber, r.LatestNumber, r.LatestStatus));
        Assert.DoesNotContain(forReader, d => d.Id == hr.Id);
        var c = forContributor.Single(d => d.Id == doc);
        Assert.Equal(("2.0", "2.1", "Draft"), (c.PublishedNumber, c.LatestNumber, c.LatestStatus));
        Assert.Contains(forContributor, d => d.Id == hr.Id); // the owner
    }

    [Fact]
    public async Task A_reader_does_not_see_documents_that_have_no_published_version_yet()
    {
        var draftOnly = await Contributor.CheckInNewOk(title: "Draft only");

        Assert.DoesNotContain(await Reader.ListOk(), d => d.Id == draftOnly.Id);
    }
}

public class DevPublishOffTests(DevPublishOffTests.Factory factory) : IClassFixture<DevPublishOffTests.Factory>
{
    public sealed class Factory : ApiFactory
    {
        protected override bool DevPublish => false;
    }

    [Fact]
    public async Task Without_the_setting_the_publish_endpoint_does_not_exist()
    {
        var doc = await new ContentClient(factory, TestUsers.Contributor).CheckInNewOk();

        var response = await new ContentClient(factory, TestUsers.Admin).Publish(doc.Id, doc.LatestVersion!.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False((await new ContentClient(factory, TestUsers.Admin).GetOk(doc.Id)).CanPublish);
    }
}
