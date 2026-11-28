using DocIntel.Api.Data;
using DocIntel.Api.Storage;
using Microsoft.EntityFrameworkCore;

namespace DocIntel.Api.Content.Pdf;

// UC-07: converts each newly published version to PDF, outside the request. A PDF
// is linked as it is; text, CSV and Markdown are rendered; other formats (ODF for
// now) record a failure against the version. A failure never affects publishing:
// the version is already published when this runs. At startup it picks up any
// version still Pending (the API scales to zero and the queue is in memory).
public sealed class PdfConverter(IServiceScopeFactory scopes, PdfQueue queue, ILogger<PdfConverter> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await EnqueuePendingAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // No database yet (unconfigured environment); conversions resume once it exists.
            logger.LogWarning(ex, "Could not look for pending PDF conversions.");
        }

        await foreach (var versionId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            await ConvertAsync(versionId, stoppingToken);
        }
    }

    public async Task<int> EnqueuePendingAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocIntelDb>();
        var pending = await db.Versions.Where(v => v.PdfStatus == PdfStatus.Pending).Select(v => v.Id).ToListAsync(ct);
        foreach (var id in pending) queue.Enqueue(id);
        return pending.Count;
    }

    public async Task ConvertAsync(Guid versionId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocIntelDb>();
        var files = scope.ServiceProvider.GetRequiredService<IFileStore>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var version = await db.Versions.SingleOrDefaultAsync(v => v.Id == versionId, ct);
        if (version is null || version.PdfStatus != PdfStatus.Pending) return;

        try
        {
            if (version.ContentType == "application/pdf")
            {
                version.PdfBlobPath = version.BlobPath;
            }
            else if (TextToPdf.CanConvert(version.ContentType))
            {
                string text;
                await using (var source = await files.OpenReadAsync(version.BlobPath, ct))
                using (var reader = new StreamReader(source))
                {
                    text = await reader.ReadToEndAsync(ct);
                }
                var pdf = TextToPdf.Convert(text, version.ContentType);
                var path = $"{version.DocumentId:N}/{version.Id:N}/rendition-{Guid.NewGuid():N}.pdf";
                await files.WriteAsync(path, new MemoryStream(pdf), "application/pdf", ct);
                version.PdfBlobPath = path;
            }
            else
            {
                throw new NotSupportedException($"Converting {version.ContentType} to PDF is not supported yet.");
            }

            version.PdfStatus = PdfStatus.Done;
            version.PdfError = null;
            version.PdfAt = clock.GetUtcNow();
            db.Record(Audit.System, Audit.Actions.PdfConverted, version.DocumentId, version.Id, new { version = version.Number }, clock);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "PDF conversion failed for version {VersionId}", versionId);
            version.PdfStatus = PdfStatus.Failed;
            version.PdfError = ex is NotSupportedException ? ex.Message : $"Conversion failed: {ex.Message}";
            version.PdfAt = clock.GetUtcNow();
            db.Record(Audit.System, Audit.Actions.PdfFailed, version.DocumentId, version.Id,
                new { version = version.Number, error = version.PdfError }, clock);
        }

        await db.SaveChangesAsync(ct);
    }
}
