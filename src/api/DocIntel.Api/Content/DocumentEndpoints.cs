using DocIntel.Api.Auth;
using Microsoft.AspNetCore.Mvc;

namespace DocIntel.Api.Content;

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this WebApplication app)
    {
        var docs = app.MapGroup("/api/documents")
            .RequireAuthorization()
            // Bearer tokens, no cookies: form posts need no antiforgery token.
            .DisableAntiforgery()
            .AddEndpointFilter(async (context, next) =>
            {
                try
                {
                    return await next(context);
                }
                catch (ContentException ex)
                {
                    return Results.Json(ex.Body, statusCode: ex.Status);
                }
            });

        // UC-01
        docs.MapPost("/", async ([FromForm] CheckInForm? form, IFormFile? file, CurrentUser user, DocumentService service, CancellationToken ct) =>
        {
            var detail = await service.CheckInNewAsync(user,
                new NewDocument(form?.Title ?? "", form?.Description, form?.CategoryId ?? 0, SplitReviewers(form?.Reviewers)), file, ct);
            return Results.Created($"/api/documents/{detail.Id}", detail);
        });

        docs.MapGet("/{id:guid}", (Guid id, CurrentUser user, DocumentService service, CancellationToken ct) =>
            service.GetDetailAsync(user, id, ct));

        // UC-02 and UC-04
        docs.MapPost("/{id:guid}/checkout", (Guid id, CurrentUser user, DocumentService service, CancellationToken ct) =>
            service.CheckOutAsync(user, id, ct));
        docs.MapDelete("/{id:guid}/checkout", (Guid id, CurrentUser user, DocumentService service, CancellationToken ct) =>
            service.CancelCheckoutAsync(user, id, ct));

        // UC-03
        docs.MapPost("/{id:guid}/versions", async (Guid id, [FromForm] NewVersionForm? form, IFormFile? file,
            CurrentUser user, DocumentService service, CancellationToken ct) =>
        {
            var reviewers = SplitReviewers(form?.Reviewers);
            return await service.CheckInVersionAsync(user, id, file, form?.Comment, reviewers.Count > 0 ? reviewers : null, ct);
        });
    }

    // Reviewers arrive as one field, comma or newline separated, in review order.
    private static List<string> SplitReviewers(string? value) =>
        (value ?? "").Split([',', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    public sealed class CheckInForm
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int CategoryId { get; set; }
        public string? Reviewers { get; set; }
    }

    public sealed class NewVersionForm
    {
        public string? Comment { get; set; }
        public string? Reviewers { get; set; }
    }
}
