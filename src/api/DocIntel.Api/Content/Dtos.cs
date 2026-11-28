namespace DocIntel.Api.Content;

public sealed record PdfDto(string Status, string? Error);

public sealed record VersionDto(
    Guid Id, string Number, string Status, string FileName, string ContentType, long SizeBytes,
    string AuthorName, DateTimeOffset CreatedAt, string? Comment, string? SourceNumber, PdfDto Pdf);

public sealed record CheckoutDto(string UserName, DateTimeOffset TakenAt, string BaseVersionNumber, bool IsMine);

public sealed record CategoryDto(int Id, string Name);

public sealed record DocumentDetail(
    Guid Id, string Title, string? Description, CategoryDto Category, string OwnerName, DateTimeOffset CreatedAt,
    bool IsRetired, DateTimeOffset? RetiredAt, string? RetiredByName,
    VersionDto? PublishedVersion, VersionDto? LatestVersion,
    IReadOnlyList<CheckoutDto> Checkouts, CheckoutDto? MyCheckout,
    bool CanEdit, bool CanAdminister, IReadOnlyList<string> Groups);

public sealed record DocumentSummary(
    Guid Id, string Title, string Category, string OwnerName, bool IsRetired,
    string? PublishedNumber, string? LatestNumber, string? LatestStatus, DateTimeOffset UpdatedAt, int CheckoutCount);

// Returned with 409 when a check-in starts from an out-of-date version.
public sealed record CheckInRefused(string Error, string LatestNumber, string LatestAuthorName, DateTimeOffset LatestCreatedAt);

// A refusal the endpoints turn into an HTTP response with this status and body.
public sealed class ContentException(int status, object body) : Exception(body.ToString())
{
    public int Status { get; } = status;
    public object Body { get; } = body;

    public static ContentException NotFound() => new(StatusCodes.Status404NotFound, new { error = "Document not found." });
    public static ContentException Forbidden(string error) => new(StatusCodes.Status403Forbidden, new { error });
    public static ContentException Invalid(string error) => new(StatusCodes.Status400BadRequest, new { error });
    public static ContentException Conflict(object body) => new(StatusCodes.Status409Conflict, body);
}
