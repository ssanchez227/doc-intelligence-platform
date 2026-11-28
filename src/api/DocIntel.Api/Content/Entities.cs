namespace DocIntel.Api.Content;

// The content model (M1). Versions are immutable: once written, a version's file,
// number and author never change; only its lifecycle status does (decision log,
// M1 decisions). Nothing is ever deleted.

public sealed class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }

    // Group keys (see GroupMap) copied onto each new document in this category.
    public List<CategoryDefaultGroup> DefaultGroups { get; set; } = [];
}

public sealed class CategoryDefaultGroup
{
    public int CategoryId { get; set; }
    public required string GroupKey { get; set; }
}

public sealed class Document
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public required string OwnerOid { get; set; }
    public required string OwnerName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // The version readers see; null until something is published.
    public Guid? PublishedVersionId { get; set; }

    // Retired documents (UC-08) are hidden from the library, search and Q&A, but
    // nothing is removed and retiring can be reversed.
    public bool IsRetired { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
    public string? RetiredByName { get; set; }

    // Changed on every write to the document, so two simultaneous check-ins
    // cannot both succeed (optimistic concurrency).
    public Guid ConcurrencyStamp { get; set; }

    public List<DocumentVersion> Versions { get; set; } = [];
    public List<DocumentGroup> Groups { get; set; } = [];
    public List<Checkout> Checkouts { get; set; } = [];
}

// A group allowed to read the document. Copied from the category at check-in;
// M2 adds editing permissions on top.
public sealed class DocumentGroup
{
    public Guid DocumentId { get; set; }
    public required string GroupKey { get; set; }
}

public enum VersionStatus
{
    Draft,
    Published,
    Archived,
}

public enum PdfStatus
{
    None,
    Pending,
    Done,
    Failed,
}

public sealed class DocumentVersion
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }

    public int Major { get; set; }
    public int Minor { get; set; }
    public VersionStatus Status { get; set; }

    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string Sha256 { get; set; }
    public required string BlobPath { get; set; }

    public required string AuthorOid { get; set; }
    public required string AuthorName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? Comment { get; set; }

    // Where this version's content came from: the approved draft for a published
    // version, the restored version for a restore. Null for an upload.
    public Guid? SourceVersionId { get; set; }

    // UC-07: the PDF rendition of a published version.
    public PdfStatus PdfStatus { get; set; }
    public string? PdfBlobPath { get; set; }
    public string? PdfError { get; set; }
    public DateTimeOffset? PdfAt { get; set; }

    public string Number => $"{Major}.{Minor}";
}

public enum CheckoutCloseReason
{
    CheckedIn,
    Cancelled,
}

// Checkouts do not lock: they record who is editing, since when and from which
// version, so others can see it and an out-of-date check-in can be refused.
public sealed class Checkout
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public required string UserOid { get; set; }
    public required string UserName { get; set; }
    public DateTimeOffset TakenAt { get; set; }
    public Guid BaseVersionId { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public CheckoutCloseReason? CloseReason { get; set; }
}

// The ordered reviewers the author chose at check-in. Saved now; M5 runs the review.
public sealed class ReviewerAssignment
{
    public Guid Id { get; set; }
    public Guid VersionId { get; set; }
    public int Order { get; set; }
    public required string ReviewerOid { get; set; }
    public string? ReviewerName { get; set; }
}

public sealed class AuditEvent
{
    public long Id { get; set; }
    public DateTimeOffset At { get; set; }
    public required string ActorOid { get; set; }
    public required string ActorName { get; set; }
    public required string Action { get; set; }
    public Guid? DocumentId { get; set; }
    public Guid? VersionId { get; set; }

    // Small JSON object with action-specific facts (version numbers, reasons).
    public string? Details { get; set; }
}
