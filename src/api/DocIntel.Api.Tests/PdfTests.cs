using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DocIntel.Api.Content;
using DocIntel.Api.Content.Pdf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocIntel.Api.Tests;

// UC-07 Convert a published document to PDF.
public class PdfTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private ContentClient Contributor => new(factory, TestUsers.Contributor);
    private ContentClient Reader => new(factory, TestUsers.Reader);
    private ContentClient Admin => new(factory, TestUsers.Admin);

    private async Task<DocumentVersion> WaitForPdf(Guid versionId)
    {
        for (var i = 0; i < 100; i++)
        {
            var v = await factory.WithDb(db => db.Versions.AsNoTracking().SingleAsync(x => x.Id == versionId));
            if (v.PdfStatus is PdfStatus.Done or PdfStatus.Failed) return v;
            await Task.Delay(50);
        }
        throw new TimeoutException("The PDF conversion did not finish.");
    }

    [Fact]
    public async Task UC07_a_published_markdown_version_gets_a_pdf_linked_to_that_exact_version()
    {
        var doc = await Contributor.CheckInNewOk(fileName: "policy.md", content: "# Travel policy\n\nBook economy for flights under six hours.");
        var published = (await Admin.PublishLatestOk(doc.Id)).PublishedVersion!;

        var done = await WaitForPdf(published.Id);

        Assert.Equal(PdfStatus.Done, done.PdfStatus);
        Assert.StartsWith($"{doc.Id:N}/{published.Id:N}/", done.PdfBlobPath);
        var response = await Reader.Open(doc.Id, published.Id, pdf: true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("policy.pdf", response.Content.Headers.ContentDisposition!.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName);
        var bytes = Encoding.Latin1.GetString(await response.Content.ReadAsByteArrayAsync());
        Assert.StartsWith("%PDF-1.4", bytes);
        Assert.Contains("(Travel policy) Tj", bytes);
        Assert.Contains("Book economy for flights under six hours.", bytes);
        Assert.Contains("version.pdf_converted", await factory.AuditActions(doc.Id));
        Assert.Equal("Done", (await Reader.GetOk(doc.Id)).PublishedVersion!.Pdf.Status);
    }

    [Fact]
    public async Task UC07_a_published_pdf_is_linked_as_it_is()
    {
        var doc = await Contributor.CheckInNewOk(fileName: "scan.pdf", content: "%PDF-1.7 pretend");
        var published = (await Admin.PublishLatestOk(doc.Id)).PublishedVersion!;

        var done = await WaitForPdf(published.Id);

        Assert.Equal(PdfStatus.Done, done.PdfStatus);
        Assert.Equal(done.BlobPath, done.PdfBlobPath);
    }

    [Fact]
    public async Task UC07_a_failed_conversion_is_recorded_and_the_version_stays_published()
    {
        var doc = await Contributor.CheckInNewOk(fileName: "minutes.odt", content: "not really odf");
        var published = (await Admin.PublishLatestOk(doc.Id)).PublishedVersion!;

        var failed = await WaitForPdf(published.Id);

        Assert.Equal(PdfStatus.Failed, failed.PdfStatus);
        Assert.Contains("not supported yet", failed.PdfError);
        Assert.Equal(VersionStatus.Published, failed.Status);
        var seen = await Reader.GetOk(doc.Id);
        Assert.Equal(("Published", "Failed"), (seen.PublishedVersion!.Status, seen.PublishedVersion.Pdf.Status));
        Assert.Equal(HttpStatusCode.Conflict, (await Reader.Open(doc.Id, published.Id, pdf: true)).StatusCode);
        Assert.Contains("version.pdf_failed", await factory.AuditActions(doc.Id));
    }

    [Fact]
    public async Task UC07_drafts_are_not_converted()
    {
        var doc = await Contributor.CheckInNewOk();

        var v = await factory.WithDb(db => db.Versions.SingleAsync(x => x.DocumentId == doc.Id));

        Assert.Equal(PdfStatus.None, v.PdfStatus);
    }

    [Fact]
    public async Task UC07_versions_left_pending_by_a_stopped_api_are_picked_up_again()
    {
        var doc = await Contributor.CheckInNewOk(fileName: "notes.txt", content: "plain notes");
        // As if the API stopped after publishing but before converting.
        var versionId = await factory.WithDb(async db =>
        {
            var v = await db.Versions.SingleAsync(x => x.DocumentId == doc.Id);
            v.Status = VersionStatus.Published;
            v.PdfStatus = PdfStatus.Pending;
            await db.SaveChangesAsync();
            return v.Id;
        });

        var found = await factory.Services.GetRequiredService<PdfConverter>().EnqueuePendingAsync(default);

        Assert.True(found >= 1);
        Assert.Equal(PdfStatus.Done, (await WaitForPdf(versionId)).PdfStatus);
    }
}

public class SimplePdfTests
{
    [Fact]
    public void The_cross_reference_table_points_at_every_object()
    {
        var bytes = TextToPdf.Convert("# Title\n\n- one\n- two\n\n```\ncode (with parens)\n```\n\nText.", "text/markdown");
        var text = Encoding.Latin1.GetString(bytes);

        var startxref = long.Parse(Regex.Match(text, @"startxref\n(\d+)").Groups[1].Value);
        Assert.StartsWith("xref", text[(int)startxref..]);
        var offsets = Regex.Matches(text[(int)startxref..], @"(\d{10}) 00000 n").Select(m => int.Parse(m.Groups[1].Value)).ToList();
        for (var i = 0; i < offsets.Count; i++) Assert.StartsWith($"{i + 1} 0 obj", text[offsets[i]..]);
        Assert.EndsWith("%%EOF\n", text);
        Assert.Contains(@"(code \(with parens\)) Tj", text);
        Assert.Contains("(- one) Tj", text);
    }

    [Fact]
    public void Long_text_flows_onto_more_pages_and_long_lines_wrap()
    {
        var longText = string.Join("\n\n", Enumerable.Range(1, 200).Select(i => $"Paragraph {i} " + string.Concat(Enumerable.Repeat("word ", 30))));

        var text = Encoding.Latin1.GetString(TextToPdf.Convert(longText, "text/markdown"));

        var pages = int.Parse(Regex.Match(text, @"/Count (\d+)").Groups[1].Value);
        Assert.True(pages > 5, $"{pages} pages");
        Assert.All(Regex.Matches(text, @"\((.*?)\) Tj").Select(m => m.Groups[1].Value), line => Assert.True(line.Length <= 80, line));
    }

    [Fact]
    public void Characters_outside_latin_1_print_as_question_marks()
    {
        var text = Encoding.Latin1.GetString(TextToPdf.Convert("café 漢字", "text/plain"));

        Assert.Contains("(café ??) Tj", text);
    }
}
