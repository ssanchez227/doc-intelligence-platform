using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocIntel.Api.Content;
using Microsoft.EntityFrameworkCore;

namespace DocIntel.Api.Tests;

// Calls the content endpoints as one test user. Methods ending in Ok assert
// success and return the parsed body; the others return the raw response.
public sealed class ContentClient(ApiFactory factory, TestUser user)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http = factory.ClientFor(user);

    public TestUser User => user;

    public Task<HttpResponseMessage> CheckInNew(string title = "Travel policy", int categoryId = 1,
        string fileName = "policy.md", string content = "# Travel policy", string? reviewers = null, string? description = null)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(title), "title" },
            { new StringContent(categoryId.ToString()), "categoryId" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes(content)), "file", fileName },
        };
        if (reviewers is not null) form.Add(new StringContent(reviewers), "reviewers");
        if (description is not null) form.Add(new StringContent(description), "description");
        return _http.PostAsync("/api/documents", form);
    }

    public async Task<DocumentDetail> CheckInNewOk(string title = "Travel policy", int categoryId = 1,
        string fileName = "policy.md", string content = "# Travel policy", string? reviewers = null) =>
        await Read<DocumentDetail>(await CheckInNew(title, categoryId, fileName, content, reviewers), HttpStatusCode.Created);

    public Task<HttpResponseMessage> Get(Guid id) => _http.GetAsync($"/api/documents/{id}");
    public async Task<DocumentDetail> GetOk(Guid id) => await Read<DocumentDetail>(await Get(id));

    public Task<HttpResponseMessage> CheckOut(Guid id) => _http.PostAsync($"/api/documents/{id}/checkout", null);
    public async Task<DocumentDetail> CheckOutOk(Guid id) => await Read<DocumentDetail>(await CheckOut(id));

    public Task<HttpResponseMessage> Cancel(Guid id) => _http.DeleteAsync($"/api/documents/{id}/checkout");
    public async Task<DocumentDetail> CancelOk(Guid id) => await Read<DocumentDetail>(await Cancel(id));

    public Task<HttpResponseMessage> CheckIn(Guid id, string content = "# Changed", string fileName = "policy.md", string? comment = null)
    {
        var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(Encoding.UTF8.GetBytes(content)), "file", fileName },
        };
        if (comment is not null) form.Add(new StringContent(comment), "comment");
        return _http.PostAsync($"/api/documents/{id}/versions", form);
    }

    public async Task<DocumentDetail> CheckInOk(Guid id, string content = "# Changed", string? comment = null) =>
        await Read<DocumentDetail>(await CheckIn(id, content, comment: comment));

    public Task<HttpResponseMessage> GetPath(string path) => _http.GetAsync(path);
    public Task<HttpResponseMessage> PostPath(string path, HttpContent? body = null) => _http.PostAsync(path, body);

    public static async Task<T> Read<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {expected}, got {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, Json)!;
    }
}

public static class TestData
{
    // Marks a version published directly in the database, for scenarios that start
    // from a published version (the publish method itself is tested separately).
    public static Task MarkPublished(this ApiFactory factory, Guid documentId, int major, int minor) =>
        factory.WithDb(async db =>
        {
            var doc = await db.Documents.SingleAsync(d => d.Id == documentId);
            var version = await db.Versions.SingleAsync(v => v.DocumentId == documentId && v.Major == major && v.Minor == minor);
            version.Status = VersionStatus.Published;
            doc.PublishedVersionId = version.Id;
            return await db.SaveChangesAsync();
        });

    public static Task<List<string>> AuditActions(this ApiFactory factory, Guid documentId) =>
        factory.WithDb(db => db.AuditEvents.Where(e => e.DocumentId == documentId).OrderBy(e => e.Id).Select(e => e.Action).ToListAsync());

    public static Task<List<string>> VersionNumbers(this ApiFactory factory, Guid documentId) =>
        factory.WithDb(async db => (await db.Versions.Where(v => v.DocumentId == documentId)
            .OrderBy(v => v.Major).ThenBy(v => v.Minor).ToListAsync()).Select(v => v.Number).ToList());
}
