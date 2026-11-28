using System.Text.Json;
using DocIntel.Api.Auth;
using DocIntel.Api.Content;

namespace DocIntel.Api.Data;

// Every action writes an audit event. Add it to the same DbContext as the change
// so both are saved in one transaction: no change without its event, and no event
// for a change that failed.
public static class Audit
{
    public static class Actions
    {
        public const string DocumentCheckedIn = "document.checked_in";
        public const string CheckoutTaken = "checkout.taken";
        public const string CheckoutCancelled = "checkout.cancelled";
        public const string VersionCheckedIn = "version.checked_in";
        public const string CheckInRefused = "version.check_in_refused";
        public const string VersionOpened = "version.opened";
        public const string VersionRestored = "version.restored";
        public const string VersionPublished = "version.published";
        public const string PdfConverted = "version.pdf_converted";
        public const string PdfFailed = "version.pdf_failed";
        public const string DocumentRetired = "document.retired";
        public const string DocumentUnretired = "document.unretired";
    }

    public static readonly CurrentUser System = new("system", "System", new HashSet<string>());

    public static AuditEvent Record(this DocIntelDb db, CurrentUser actor, string action,
        Guid? documentId = null, Guid? versionId = null, object? details = null, TimeProvider? clock = null)
    {
        var ev = new AuditEvent
        {
            At = (clock ?? TimeProvider.System).GetUtcNow(),
            ActorOid = actor.Oid,
            ActorName = actor.Name,
            Action = action,
            DocumentId = documentId,
            VersionId = versionId,
            Details = details is null ? null : JsonSerializer.Serialize(details, JsonSerializerOptions.Web),
        };
        db.AuditEvents.Add(ev);
        return ev;
    }
}
