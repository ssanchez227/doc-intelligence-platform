using System.Security.Cryptography;
using DocIntel.Api.Auth;
using DocIntel.Api.Data;
using DocIntel.Api.Storage;
using Microsoft.EntityFrameworkCore;

namespace DocIntel.Api.Content;

public sealed record NewDocument(string Title, string? Description, int CategoryId, IReadOnlyList<string> Reviewers);

// The content lifecycle (M1). Every method that changes something records an audit
// event in the same SaveChanges as the change.
public sealed class DocumentService(DocIntelDb db, IFileStore files, TimeProvider clock, ContentFeatures features)
{
    // UC-01: a new document at 0.1, as a draft, with its category's default groups.
    public async Task<DocumentDetail> CheckInNewAsync(CurrentUser user, NewDocument input, IFormFile? file, CancellationToken ct)
    {
        RequireContributor(user);
        if (string.IsNullOrWhiteSpace(input.Title)) throw ContentException.Invalid("Give the document a title.");
        if (FileRules.Check(file) is { } fileError) throw ContentException.Invalid(fileError);
        var category = await db.Categories.Include(c => c.DefaultGroups).SingleOrDefaultAsync(c => c.Id == input.CategoryId, ct)
            ?? throw ContentException.Invalid("Choose a category.");

        var now = clock.GetUtcNow();
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            Title = input.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
            CategoryId = category.Id,
            OwnerOid = user.Oid,
            OwnerName = user.Name,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid(),
            Groups = category.DefaultGroups.Select(g => new DocumentGroup { GroupKey = g.GroupKey }).ToList(),
        };
        var version = await StoreVersionAsync(doc.Id, VersionNumbers.First, file!, user, null, ct);
        db.Documents.Add(doc);
        db.Versions.Add(version);
        AssignReviewers(version.Id, input.Reviewers);
        db.Record(user, Audit.Actions.DocumentCheckedIn, doc.Id, version.Id,
            new { version = version.Number, category = category.Name, fileName = version.FileName }, clock);
        await db.SaveChangesAsync(ct);

        return await GetDetailAsync(user, doc.Id, ct);
    }

    public async Task<DocumentDetail> GetDetailAsync(CurrentUser user, Guid id, CancellationToken ct)
    {
        var doc = await LoadReadableAsync(user, id, ct);
        var versions = await db.Versions.Where(v => v.DocumentId == id).ToListAsync(ct);
        var byId = versions.ToDictionary(v => v.Id);
        var checkouts = await db.Checkouts.Where(c => c.DocumentId == id && c.ClosedAt == null)
            .OrderBy(c => c.TakenAt).ToListAsync(ct);

        var visible = versions.Where(v => CanSeeVersion(user, v));
        var latest = visible.OrderByDescending(v => v.Major).ThenByDescending(v => v.Minor).FirstOrDefault();
        var published = doc.PublishedVersionId is { } pid ? byId[pid] : null;
        var checkoutDtos = checkouts.Select(c => new CheckoutDto(c.UserName, c.TakenAt, byId[c.BaseVersionId].Number, c.UserOid == user.Oid)).ToList();

        return new DocumentDetail(
            doc.Id, doc.Title, doc.Description, new CategoryDto(doc.Category!.Id, doc.Category.Name), doc.OwnerName, doc.CreatedAt,
            doc.IsRetired, doc.RetiredAt, doc.RetiredByName,
            published is null ? null : ToDto(published, byId), latest is null ? null : ToDto(latest, byId),
            checkoutDtos, checkoutDtos.SingleOrDefault(c => c.IsMine),
            CanEdit(user, doc), user.IsAdministrator,
            // The dev-only publish endpoint applies to the latest version when it is a draft.
            features.DevPublish && user.IsAdministrator && !doc.IsRetired && latest?.Status == VersionStatus.Draft,
            doc.Groups.Select(g => g.GroupKey).Order().ToList());
    }

    // UC-02: checking out records who, when and from which version. It never blocks.
    // Checking out again while holding an out-of-date checkout moves it to the
    // latest version (after a refused check-in, this is how the user starts again).
    public async Task<DocumentDetail> CheckOutAsync(CurrentUser user, Guid id, CancellationToken ct)
    {
        var doc = await LoadEditableAsync(user, id, ct);
        var open = await OpenCheckoutAsync(user, id, ct);
        var latest = await LatestVersionAsync(id, ct);
        if (open is not null && open.BaseVersionId != latest.Id)
        {
            open.BaseVersionId = latest.Id;
            open.TakenAt = clock.GetUtcNow();
            db.Record(user, Audit.Actions.CheckoutTaken, doc.Id, latest.Id, new { baseVersion = latest.Number, restarted = true }, clock);
            await db.SaveChangesAsync(ct);
        }
        else if (open is null)
        {
            db.Checkouts.Add(new Checkout
            {
                Id = Guid.NewGuid(),
                DocumentId = doc.Id,
                UserOid = user.Oid,
                UserName = user.Name,
                TakenAt = clock.GetUtcNow(),
                BaseVersionId = latest.Id,
            });
            db.Record(user, Audit.Actions.CheckoutTaken, doc.Id, latest.Id, new { baseVersion = latest.Number }, clock);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // The same user checked out twice at once; the other request won. Nothing to do.
                db.ChangeTracker.Clear();
            }
        }
        return await GetDetailAsync(user, id, ct);
    }

    // UC-04: closes the caller's checkout; no version is created.
    public async Task<DocumentDetail> CancelCheckoutAsync(CurrentUser user, Guid id, CancellationToken ct)
    {
        var doc = await LoadReadableAsync(user, id, ct);
        var checkout = await OpenCheckoutAsync(user, id, ct)
            ?? throw ContentException.Conflict(new { error = "You do not have this document checked out." });

        checkout.ClosedAt = clock.GetUtcNow();
        checkout.CloseReason = CheckoutCloseReason.Cancelled;
        db.Record(user, Audit.Actions.CheckoutCancelled, doc.Id, checkout.BaseVersionId, null, clock);
        await db.SaveChangesAsync(ct);
        return await GetDetailAsync(user, id, ct);
    }

    // UC-03: a new draft at the next minor number, if the caller's checkout started
    // from the latest version. Otherwise it is refused and nothing is created.
    public async Task<DocumentDetail> CheckInVersionAsync(
        CurrentUser user, Guid id, IFormFile? file, string? comment, IReadOnlyList<string>? reviewers, CancellationToken ct)
    {
        var doc = await LoadEditableAsync(user, id, ct);
        var checkout = await OpenCheckoutAsync(user, id, ct)
            ?? throw ContentException.Conflict(new { error = "Check the document out before checking in a new version." });
        if (FileRules.Check(file) is { } fileError) throw ContentException.Invalid(fileError);

        var latest = await LatestVersionAsync(id, ct);
        if (latest.Id != checkout.BaseVersionId)
        {
            await RefuseAsync(user, doc.Id, checkout.BaseVersionId, latest, ct);
        }

        var number = VersionNumbers.NextMinor([(latest.Major, latest.Minor)]);
        var version = await StoreVersionAsync(id, number, file!, user, null, ct, comment);
        await CommitNewVersionAsync(user, doc, checkout, version, reviewers, Audit.Actions.VersionCheckedIn,
            new { version = version.Number, previous = latest.Number }, ct);
        return await GetDetailAsync(user, id, ct);
    }

    // The library: documents the caller may read. Retired ones only when asked for.
    public async Task<IReadOnlyList<DocumentSummary>> ListAsync(CurrentUser user, bool retired, CancellationToken ct)
    {
        var keys = user.GroupKeys.ToList();
        var docs = await db.Documents
            .Where(d => d.IsRetired == retired)
            .Where(d => user.IsAdministrator || d.OwnerOid == user.Oid || d.Groups.Any(g => keys.Contains(g.GroupKey)))
            .Select(d => new { d.Id, d.Title, Category = d.Category!.Name, d.OwnerName, d.IsRetired, d.PublishedVersionId })
            .ToListAsync(ct);
        var ids = docs.Select(d => d.Id).ToList();
        var versions = (await db.Versions.Where(v => ids.Contains(v.DocumentId)).ToListAsync(ct))
            .Where(v => CanSeeVersion(user, v)).ToLookup(v => v.DocumentId);
        var checkouts = (await db.Checkouts.Where(c => ids.Contains(c.DocumentId) && c.ClosedAt == null)
            .Select(c => c.DocumentId).ToListAsync(ct)).CountBy(x => x).ToDictionary();

        return docs.Select(d =>
            {
                var mine = versions[d.Id].OrderByDescending(v => v.Major).ThenByDescending(v => v.Minor).ToList();
                var latest = mine.FirstOrDefault();
                var published = mine.FirstOrDefault(v => v.Id == d.PublishedVersionId);
                return new DocumentSummary(d.Id, d.Title, d.Category, d.OwnerName, d.IsRetired,
                    published?.Number, latest?.Number, latest?.Status.ToString(),
                    mine.Count > 0 ? mine.Max(v => v.CreatedAt) : default, checkouts.GetValueOrDefault(d.Id));
            })
            .Where(s => s.LatestNumber is not null)
            .OrderByDescending(s => s.UpdatedAt)
            .ToList();
    }

    // UC-05: every version the caller may open, newest first.
    public async Task<IReadOnlyList<VersionDto>> HistoryAsync(CurrentUser user, Guid id, CancellationToken ct)
    {
        await LoadReadableAsync(user, id, ct);
        var versions = await db.Versions.Where(v => v.DocumentId == id).ToListAsync(ct);
        var byId = versions.ToDictionary(v => v.Id);
        return versions.Where(v => CanSeeVersion(user, v))
            .OrderByDescending(v => v.Major).ThenByDescending(v => v.Minor)
            .Select(v => ToDto(v, byId)).ToList();
    }

    public sealed record OpenedFile(Stream Content, string ContentType, string FileName);

    // UC-05: open one version's file (or its PDF, UC-07). A version the caller may not
    // see answers exactly like one that does not exist. Opening is audited.
    public async Task<OpenedFile> OpenVersionAsync(CurrentUser user, Guid id, Guid versionId, bool pdf, CancellationToken ct)
    {
        await LoadReadableAsync(user, id, ct);
        var version = await db.Versions.SingleOrDefaultAsync(v => v.Id == versionId && v.DocumentId == id, ct);
        if (version is null || !CanSeeVersion(user, version)) throw ContentException.NotFound();
        if (pdf && version.PdfStatus != PdfStatus.Done)
        {
            throw ContentException.Conflict(new { error = "This version has no PDF.", pdf = version.PdfStatus.ToString() });
        }

        var path = pdf ? version.PdfBlobPath! : version.BlobPath;
        var stream = await files.OpenReadAsync(path, ct);
        db.Record(user, Audit.Actions.VersionOpened, id, version.Id, new { version = version.Number, pdf }, clock);
        await db.SaveChangesAsync(ct);
        var name = pdf ? Path.ChangeExtension(version.FileName, ".pdf") : version.FileName;
        return new OpenedFile(stream, pdf ? "application/pdf" : version.ContentType, name);
    }

    // Publishing (wired to the review in M5; until then a dev-only endpoint calls it).
    // The latest draft becomes a new version with the next major number, pointing at
    // the same file. The draft keeps its number; the previous published version
    // becomes Archived.
    public async Task<DocumentVersion> PublishAsync(CurrentUser actor, Guid id, Guid draftId, CancellationToken ct)
    {
        var doc = await db.Documents.SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw ContentException.NotFound();
        if (doc.IsRetired) throw ContentException.Conflict(new { error = "This document is retired." });
        var versions = await db.Versions.Where(v => v.DocumentId == id).ToListAsync(ct);
        var draft = versions.SingleOrDefault(v => v.Id == draftId) ?? throw ContentException.NotFound();
        var latest = versions.OrderByDescending(v => v.Major).ThenByDescending(v => v.Minor).First();
        if (draft.Status != VersionStatus.Draft || draft.Id != latest.Id)
        {
            throw ContentException.Conflict(new { error = $"Only the latest draft can be published; the latest version is {latest.Number}." });
        }

        var number = VersionNumbers.NextMajor(versions.Select(v => (v.Major, v.Minor)));
        var published = new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = id,
            Major = number.Major,
            Minor = number.Minor,
            Status = VersionStatus.Published,
            FileName = draft.FileName,
            ContentType = draft.ContentType,
            SizeBytes = draft.SizeBytes,
            Sha256 = draft.Sha256,
            BlobPath = draft.BlobPath,
            AuthorOid = draft.AuthorOid,
            AuthorName = draft.AuthorName,
            CreatedAt = clock.GetUtcNow(),
            Comment = draft.Comment,
            SourceVersionId = draft.Id,
            PdfStatus = PdfStatus.Pending,
        };
        var previous = versions.SingleOrDefault(v => v.Id == doc.PublishedVersionId);
        if (previous is not null) previous.Status = VersionStatus.Archived;
        db.Versions.Add(published);
        doc.PublishedVersionId = published.Id;
        doc.ConcurrencyStamp = Guid.NewGuid();
        db.Record(actor, Audit.Actions.VersionPublished, id, published.Id,
            new { version = published.Number, from = draft.Number, archived = previous?.Number }, clock);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            throw ContentException.Conflict(new { error = "The document changed while publishing. Try again." });
        }
        return published;
    }

    // UC-06: the old version's content becomes a new draft at the next minor number.
    // Nothing earlier changes, and the new draft goes through review like any other
    // (M5). It needs no checkout; others' open checkouts become out of date.
    public async Task<DocumentDetail> RestoreAsync(CurrentUser user, Guid id, Guid versionId, CancellationToken ct)
    {
        var doc = await LoadEditableAsync(user, id, ct);
        var versions = await db.Versions.Where(v => v.DocumentId == id).ToListAsync(ct);
        var old = versions.SingleOrDefault(v => v.Id == versionId);
        if (old is null || !CanSeeVersion(user, old)) throw ContentException.NotFound();
        var latest = versions.OrderByDescending(v => v.Major).ThenByDescending(v => v.Minor).First();
        if (old.Id == latest.Id) throw ContentException.Conflict(new { error = $"Version {old.Number} is already the latest." });

        var number = VersionNumbers.NextMinor(versions.Select(v => (v.Major, v.Minor)));
        var restored = new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = id,
            Major = number.Major,
            Minor = number.Minor,
            Status = VersionStatus.Draft,
            FileName = old.FileName,
            ContentType = old.ContentType,
            SizeBytes = old.SizeBytes,
            Sha256 = old.Sha256,
            // Same immutable file: a restore copies the reference, not the bytes.
            BlobPath = old.BlobPath,
            AuthorOid = user.Oid,
            AuthorName = user.Name,
            CreatedAt = clock.GetUtcNow(),
            Comment = $"Restored from version {old.Number}",
            SourceVersionId = old.Id,
        };
        await CommitNewVersionAsync(user, doc, null, restored, null, Audit.Actions.VersionRestored,
            new { version = restored.Number, restoredFrom = old.Number, previousLatest = latest.Number }, ct);
        return await GetDetailAsync(user, id, ct);
    }

    // UC-08: retiring hides the document from the library (and from search and Q&A
    // once they exist) and stops changes. Versions and audit trail stay readable,
    // and an administrator can reverse it. Nothing is deleted.
    public async Task<DocumentDetail> SetRetiredAsync(CurrentUser user, Guid id, bool retire, string? reason, CancellationToken ct)
    {
        if (!user.IsAdministrator) throw ContentException.Forbidden("Only administrators can retire documents.");
        var doc = await LoadReadableAsync(user, id, ct);
        if (doc.IsRetired == retire)
        {
            throw ContentException.Conflict(new { error = retire ? "The document is already retired." : "The document is not retired." });
        }

        doc.IsRetired = retire;
        doc.RetiredAt = retire ? clock.GetUtcNow() : null;
        doc.RetiredByName = retire ? user.Name : null;
        doc.ConcurrencyStamp = Guid.NewGuid();
        db.Record(user, retire ? Audit.Actions.DocumentRetired : Audit.Actions.DocumentUnretired, id, doc.PublishedVersionId,
            new { reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim() }, clock);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw ContentException.Conflict(new { error = "The document changed at the same moment. Try again." });
        }
        return await GetDetailAsync(user, id, ct);
    }

    // ---- shared steps ----

    private async Task CommitNewVersionAsync(CurrentUser user, Document doc, Checkout? checkout, DocumentVersion version,
        IReadOnlyList<string>? reviewers, string action, object details, CancellationToken ct)
    {
        db.Versions.Add(version);
        if (reviewers is { Count: > 0 })
        {
            AssignReviewers(version.Id, reviewers);
        }
        else
        {
            await CopyReviewersAsync(doc.Id, version.Id, ct);
        }
        if (checkout is not null)
        {
            checkout.ClosedAt = clock.GetUtcNow();
            checkout.CloseReason = CheckoutCloseReason.CheckedIn;
        }
        // Changing the stamp makes a simultaneous write from the same starting point fail.
        doc.ConcurrencyStamp = Guid.NewGuid();
        db.Record(user, action, doc.Id, version.Id, details, clock);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Someone else's version landed between our check and our save. The file
            // we stored stays (nothing is deleted) but no version points at it.
            db.ChangeTracker.Clear();
            if (checkout is null)
            {
                throw ContentException.Conflict(new { error = "The document changed at the same moment. Try again." });
            }
            var newest = await LatestVersionAsync(doc.Id, ct);
            await RefuseAsync(user, doc.Id, checkout.BaseVersionId, newest, ct);
        }
    }

    private async Task RefuseAsync(CurrentUser user, Guid documentId, Guid? baseVersionId, DocumentVersion latest, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        db.Record(user, Audit.Actions.CheckInRefused, documentId, baseVersionId,
            new { latest = latest.Number, latestAuthor = latest.AuthorName }, clock);
        await db.SaveChangesAsync(ct);
        throw ContentException.Conflict(new CheckInRefused(
            $"{latest.AuthorName} checked in version {latest.Number} after you checked out. Check out again to start from the latest version.",
            latest.Number, latest.AuthorName, latest.CreatedAt));
    }

    private async Task<DocumentVersion> StoreVersionAsync(Guid documentId, (int Major, int Minor) number, IFormFile file,
        CurrentUser user, Guid? sourceVersionId, CancellationToken ct, string? comment = null)
    {
        var versionId = Guid.NewGuid();
        var name = FileRules.SafeName(file.FileName);
        var contentType = FileRules.ContentTypeFor(name);
        var path = $"{documentId:N}/{versionId:N}/{name}";

        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var hash = Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
        buffer.Position = 0;
        await files.WriteAsync(path, buffer, contentType, ct);

        return new DocumentVersion
        {
            Id = versionId,
            DocumentId = documentId,
            Major = number.Major,
            Minor = number.Minor,
            Status = VersionStatus.Draft,
            FileName = name,
            ContentType = contentType,
            SizeBytes = buffer.Length,
            Sha256 = hash,
            BlobPath = path,
            AuthorOid = user.Oid,
            AuthorName = user.Name,
            CreatedAt = clock.GetUtcNow(),
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            SourceVersionId = sourceVersionId,
        };
    }

    private void AssignReviewers(Guid versionId, IReadOnlyList<string> reviewers)
    {
        var order = 1;
        foreach (var reviewer in reviewers.Select(r => r.Trim()).Where(r => r.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            db.ReviewerAssignments.Add(new ReviewerAssignment
            {
                Id = Guid.NewGuid(), VersionId = versionId, Order = order++, ReviewerOid = reviewer,
            });
        }
    }

    // A new version keeps the reviewers of the version before it unless new ones are given.
    private async Task CopyReviewersAsync(Guid documentId, Guid versionId, CancellationToken ct)
    {
        var previous = await db.ReviewerAssignments
            .Join(db.Versions.Where(v => v.DocumentId == documentId), r => r.VersionId, v => v.Id, (r, v) => new { r, v })
            .OrderByDescending(x => x.v.Major).ThenByDescending(x => x.v.Minor).ThenBy(x => x.r.Order)
            .Select(x => new { x.r.VersionId, x.r.ReviewerOid, x.r.ReviewerName, x.r.Order })
            .ToListAsync(ct);
        var last = previous.FirstOrDefault()?.VersionId;
        foreach (var r in previous.Where(p => p.VersionId == last))
        {
            db.ReviewerAssignments.Add(new ReviewerAssignment
            {
                Id = Guid.NewGuid(), VersionId = versionId, Order = r.Order, ReviewerOid = r.ReviewerOid, ReviewerName = r.ReviewerName,
            });
        }
    }

    internal async Task<Document> LoadReadableAsync(CurrentUser user, Guid id, CancellationToken ct)
    {
        var doc = await db.Documents.Include(d => d.Groups).Include(d => d.Category).SingleOrDefaultAsync(d => d.Id == id, ct);
        // Not found and not allowed look the same, so nothing is revealed (M2 adds
        // the denied-access experience, UC-16).
        // The owner can always read their own document, even in a category whose
        // groups they are not in.
        if (doc is null || (doc.OwnerOid != user.Oid && !user.CanRead(doc.Groups.Select(g => g.GroupKey))))
        {
            throw ContentException.NotFound();
        }
        return doc;
    }

    private async Task<Document> LoadEditableAsync(CurrentUser user, Guid id, CancellationToken ct)
    {
        var doc = await LoadReadableAsync(user, id, ct);
        if (doc.IsRetired) throw ContentException.Conflict(new { error = "This document is retired. An administrator can reverse that." });
        if (!CanEdit(user, doc)) throw ContentException.Forbidden("Only contributors can change documents.");
        return doc;
    }

    private Task<Checkout?> OpenCheckoutAsync(CurrentUser user, Guid id, CancellationToken ct) =>
        db.Checkouts.SingleOrDefaultAsync(c => c.DocumentId == id && c.UserOid == user.Oid && c.ClosedAt == null, ct);

    internal Task<DocumentVersion> LatestVersionAsync(Guid id, CancellationToken ct) =>
        db.Versions.Where(v => v.DocumentId == id).OrderByDescending(v => v.Major).ThenByDescending(v => v.Minor).FirstAsync(ct);

    private static void RequireContributor(CurrentUser user)
    {
        if (!user.IsContributor) throw ContentException.Forbidden("Only contributors can check in documents.");
    }

    private static bool CanEdit(CurrentUser user, Document doc) => user.IsContributor && !doc.IsRetired;

    // Drafts are for contributors and administrators; readers see published and
    // archived versions only (the published version stays what they read).
    internal static bool CanSeeVersion(CurrentUser user, DocumentVersion v) =>
        v.Status != VersionStatus.Draft || user.IsContributor || user.IsAdministrator;

    internal static VersionDto ToDto(DocumentVersion v, IReadOnlyDictionary<Guid, DocumentVersion> all) => new(
        v.Id, v.Number, v.Status.ToString(), v.FileName, v.ContentType, v.SizeBytes, v.AuthorName, v.CreatedAt, v.Comment,
        v.SourceVersionId is { } s && all.TryGetValue(s, out var source) ? source.Number : null,
        new PdfDto(v.PdfStatus.ToString(), v.PdfError));
}
