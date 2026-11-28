using System.Net;
using System.Text;
using DocIntel.Api.Content;
using DocIntel.Api.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocIntel.Api.Tests;

// UC-01 to UC-04: check in, check out (optimistic concurrency), check in a new
// version, cancel. Each test makes its own document, so they share one factory.
public class CheckInOutTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private ContentClient Contributor => new(factory, TestUsers.Contributor);
    private ContentClient Contributor2 => new(factory, TestUsers.Contributor2);
    private ContentClient Reader => new(factory, TestUsers.Reader);

    // ---- UC-01 Check in a new document ----

    [Fact]
    public async Task UC01_new_document_is_a_draft_at_0_1_with_category_groups_file_and_reviewers()
    {
        var doc = await Contributor.CheckInNewOk("Expense rules", categoryId: 3, fileName: "expenses.md",
            content: "# Expenses", reviewers: "oid-reviewer-1, oid-reviewer-2");

        Assert.Equal("0.1", doc.LatestVersion!.Number);
        Assert.Equal("Draft", doc.LatestVersion.Status);
        Assert.Null(doc.PublishedVersion);
        Assert.Equal("Finance", doc.Category.Name);
        Assert.Equal(["finance"], doc.Groups);
        Assert.Equal("Test Contributor", doc.OwnerName);
        Assert.Equal("text/markdown", doc.LatestVersion.ContentType);

        var (path, reviewers) = await factory.WithDb(async db =>
        {
            var v = await db.Versions.SingleAsync(x => x.DocumentId == doc.Id);
            var r = await db.ReviewerAssignments.Where(x => x.VersionId == v.Id).OrderBy(x => x.Order).Select(x => x.ReviewerOid).ToListAsync();
            return (v.BlobPath, r);
        });
        Assert.Equal(["oid-reviewer-1", "oid-reviewer-2"], reviewers);
        await using var stored = await factory.Services.GetRequiredService<IFileStore>().OpenReadAsync(path, default);
        Assert.Equal("# Expenses", await new StreamReader(stored, Encoding.UTF8).ReadToEndAsync());
        Assert.Equal(["document.checked_in"], await factory.AuditActions(doc.Id));
    }

    [Fact]
    public async Task UC01_a_reader_cannot_check_in()
    {
        var response = await Reader.CheckInNew();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("report.docx")]
    [InlineData("sheet.xlsx")]
    [InlineData("noextension")]
    public async Task UC01_proprietary_or_unknown_formats_are_refused(string fileName)
    {
        var before = await factory.WithDb(db => db.Documents.CountAsync());

        var response = await Contributor.CheckInNew(title: $"Refused {fileName}", fileName: fileName);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await factory.WithDb(db => db.Documents.CountAsync()));
    }

    [Fact]
    public async Task UC01_a_title_and_a_known_category_are_required()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Contributor.CheckInNew(title: " ")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Contributor.CheckInNew(categoryId: 999)).StatusCode);
    }

    [Fact]
    public async Task UC01_a_client_supplied_path_is_reduced_to_the_file_name()
    {
        var doc = await Contributor.CheckInNewOk(fileName: "../../etc/notes.txt");

        Assert.Equal("notes.txt", doc.LatestVersion!.FileName);
    }

    // ---- UC-02 Check out a document to edit ----

    [Fact]
    public async Task UC02_checkout_records_owner_time_and_starting_version_and_never_blocks()
    {
        var doc = await Contributor.CheckInNewOk();

        var mine = await Contributor.CheckOutOk(doc.Id);
        var theirs = await Contributor2.CheckOutOk(doc.Id);

        Assert.Equal("Test Contributor", mine.MyCheckout!.UserName);
        Assert.Equal("0.1", mine.MyCheckout.BaseVersionNumber);
        Assert.True(DateTimeOffset.UtcNow - mine.MyCheckout.TakenAt < TimeSpan.FromMinutes(1));
        // The second contributor is not blocked, and sees who else has it checked out.
        Assert.Equal(["Test Contributor", "Second Contributor"], theirs.Checkouts.Select(c => c.UserName));
        Assert.Equal("Second Contributor", theirs.MyCheckout!.UserName);
        Assert.Equal(["document.checked_in", "checkout.taken", "checkout.taken"], await factory.AuditActions(doc.Id));
    }

    [Fact]
    public async Task UC02_checking_out_twice_keeps_one_checkout()
    {
        var doc = await Contributor.CheckInNewOk();

        await Contributor.CheckOutOk(doc.Id);
        var again = await Contributor.CheckOutOk(doc.Id);

        Assert.Single(again.Checkouts);
    }

    [Fact]
    public async Task UC02_readers_keep_seeing_the_published_version_while_it_is_checked_out()
    {
        var doc = await Contributor.CheckInNewOk();
        await factory.WithDb(async db =>
        {
            var v = await db.Versions.SingleAsync(x => x.DocumentId == doc.Id);
            v.Major = 1; v.Minor = 0;
            return await db.SaveChangesAsync();
        });
        await factory.MarkPublished(doc.Id, 1, 0);
        await Contributor.CheckOutOk(doc.Id);
        await Contributor.CheckInOk(doc.Id);

        var seen = await Reader.GetOk(doc.Id);

        Assert.Equal("1.0", seen.PublishedVersion!.Number);
        Assert.Equal("1.0", seen.LatestVersion!.Number); // the 1.1 draft is not shown to readers
        Assert.False(seen.CanEdit);
    }

    [Fact]
    public async Task UC02_a_reader_cannot_check_out()
    {
        var doc = await Contributor.CheckInNewOk();

        Assert.Equal(HttpStatusCode.Forbidden, (await Reader.CheckOut(doc.Id)).StatusCode);
    }

    [Fact]
    public async Task UC02_someone_outside_the_documents_groups_cannot_see_it_exists()
    {
        var doc = await Contributor.CheckInNewOk(categoryId: 4); // HR only

        Assert.Equal(HttpStatusCode.OK, (await Contributor.Get(doc.Id)).StatusCode); // the owner
        Assert.Equal(HttpStatusCode.NotFound, (await Contributor2.Get(doc.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Reader.Get(doc.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await new ContentClient(factory, TestUsers.Outsider).Get(doc.Id)).StatusCode);
    }

    // ---- UC-03 Check in a new version ----

    [Fact]
    public async Task UC03_check_in_after_1_0_creates_draft_1_1_closes_checkout_and_keeps_1_0_published()
    {
        var doc = await PublishedAt1_0();
        await Contributor.CheckOutOk(doc);

        var after = await Contributor.CheckInOk(doc, "# Version 1.1", comment: "Updated rates");

        Assert.Equal("1.1", after.LatestVersion!.Number);
        Assert.Equal("Draft", after.LatestVersion.Status);
        Assert.Equal("Updated rates", after.LatestVersion.Comment);
        Assert.Equal("1.0", after.PublishedVersion!.Number);
        Assert.Empty(after.Checkouts);
        Assert.Equal(["0.1", "1.0", "1.1"], await factory.VersionNumbers(doc));
    }

    [Fact]
    public async Task UC03_drafts_take_consecutive_minor_numbers()
    {
        var doc = await Contributor.CheckInNewOk();
        for (var i = 0; i < 2; i++)
        {
            await Contributor.CheckOutOk(doc.Id);
            await Contributor.CheckInOk(doc.Id, $"# draft {i}");
        }

        Assert.Equal(["0.1", "0.2", "0.3"], await factory.VersionNumbers(doc.Id));
    }

    [Fact]
    public async Task UC03_a_second_users_out_of_date_check_in_is_refused_and_told_who_checked_in_since()
    {
        var doc = await PublishedAt1_0();
        await Contributor.CheckOutOk(doc);
        await Contributor2.CheckOutOk(doc);
        await Contributor.CheckInOk(doc, "# First in");

        var refused = await Contributor2.CheckIn(doc, "# Second, from 1.0");

        var body = await ContentClient.Read<CheckInRefused>(refused, HttpStatusCode.Conflict);
        Assert.Equal(("1.1", "Test Contributor"), (body.LatestNumber, body.LatestAuthorName));
        Assert.Contains("Test Contributor checked in version 1.1", body.Error);
        Assert.Equal(["0.1", "1.0", "1.1"], await factory.VersionNumbers(doc));
        // Their checkout stays open, still pointing at 1.0.
        var seen = await Contributor2.GetOk(doc);
        Assert.Equal("1.0", seen.MyCheckout!.BaseVersionNumber);
        Assert.Contains("version.check_in_refused", await factory.AuditActions(doc));
    }

    [Fact]
    public async Task UC03_after_a_refusal_checking_out_again_starts_from_the_latest_version()
    {
        var doc = await PublishedAt1_0();
        await Contributor.CheckOutOk(doc);
        await Contributor2.CheckOutOk(doc);
        await Contributor.CheckInOk(doc);
        await Contributor2.CheckIn(doc);

        var restarted = await Contributor2.CheckOutOk(doc);
        var after = await Contributor2.CheckInOk(doc, "# Merged");

        Assert.Equal("1.1", restarted.MyCheckout!.BaseVersionNumber);
        Assert.Equal("1.2", after.LatestVersion!.Number);
        Assert.Equal("Second Contributor", after.LatestVersion.AuthorName);
    }

    [Fact]
    public async Task UC03_simultaneous_check_ins_from_the_same_version_create_only_one()
    {
        var doc = await Contributor.CheckInNewOk();
        var users = Enumerable.Range(0, 6).Select(i => new ContentClient(factory, new TestUser($"oid-c{i}", $"C{i}", "contributors"))).ToList();
        foreach (var u in users) await u.CheckOutOk(doc.Id);

        var responses = await Task.WhenAll(users.Select(u => u.CheckIn(doc.Id, $"# from {u.User.Name}")));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(["0.1", "0.2"], await factory.VersionNumbers(doc.Id));
    }

    [Fact]
    public async Task UC03_checking_in_without_a_checkout_is_refused()
    {
        var doc = await Contributor.CheckInNewOk();

        Assert.Equal(HttpStatusCode.Conflict, (await Contributor.CheckIn(doc.Id)).StatusCode);
        Assert.Equal(["0.1"], await factory.VersionNumbers(doc.Id));
    }

    // ---- UC-04 Cancel a checkout ----

    [Fact]
    public async Task UC04_cancel_closes_the_checkout_creates_no_version_and_is_audited()
    {
        var doc = await Contributor.CheckInNewOk();
        await Contributor.CheckOutOk(doc.Id);

        var after = await Contributor.CancelOk(doc.Id);

        Assert.Empty(after.Checkouts);
        Assert.Equal(["0.1"], await factory.VersionNumbers(doc.Id));
        Assert.Equal(["document.checked_in", "checkout.taken", "checkout.cancelled"], await factory.AuditActions(doc.Id));
        var closed = await factory.WithDb(db => db.Checkouts.SingleAsync(c => c.DocumentId == doc.Id));
        Assert.Equal(CheckoutCloseReason.Cancelled, closed.CloseReason);
    }

    [Fact]
    public async Task UC04_cancelling_only_affects_your_own_checkout()
    {
        var doc = await Contributor.CheckInNewOk();
        await Contributor.CheckOutOk(doc.Id);

        Assert.Equal(HttpStatusCode.Conflict, (await Contributor2.Cancel(doc.Id)).StatusCode);
        Assert.Single((await Contributor.GetOk(doc.Id)).Checkouts);
    }

    // A document whose only version is 1.0 and published.
    private async Task<Guid> PublishedAt1_0()
    {
        var doc = await Contributor.CheckInNewOk();
        await factory.WithDb(async db =>
        {
            var v = await db.Versions.SingleAsync(x => x.DocumentId == doc.Id);
            db.Versions.Add(new DocumentVersion
            {
                Id = Guid.NewGuid(), DocumentId = doc.Id, Major = 1, Minor = 0, Status = VersionStatus.Draft,
                FileName = v.FileName, ContentType = v.ContentType, SizeBytes = v.SizeBytes, Sha256 = v.Sha256,
                BlobPath = v.BlobPath, AuthorOid = v.AuthorOid, AuthorName = v.AuthorName, CreatedAt = v.CreatedAt, SourceVersionId = v.Id,
            });
            return await db.SaveChangesAsync();
        });
        await factory.MarkPublished(doc.Id, 1, 0);
        return doc.Id;
    }
}
