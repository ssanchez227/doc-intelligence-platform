using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace DocIntel.Api.Storage;

// Where document files live. Paths are written once and never overwritten
// (versions are immutable) and nothing is deleted.
public interface IFileStore
{
    Task WriteAsync(string path, Stream content, string contentType, CancellationToken ct);
    Task<Stream> OpenReadAsync(string path, CancellationToken ct);
}

public sealed class StorageOptions
{
    // "Blob" (Azure, managed identity) or "Local" (a folder, for local runs).
    public string Provider { get; set; } = "Local";
    public string? BlobServiceUri { get; set; }
    public string Container { get; set; } = "documents";
    // Client ID of the user-assigned identity, when there is more than one.
    public string? ManagedIdentityClientId { get; set; }
    public string LocalPath { get; set; } = "App_Data/files";
}

public sealed class BlobFileStore : IFileStore
{
    private readonly BlobContainerClient _container;

    public BlobFileStore(StorageOptions options)
    {
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = options.ManagedIdentityClientId,
        });
        var service = new BlobServiceClient(new Uri(options.BlobServiceUri
            ?? throw new InvalidOperationException("Storage:BlobServiceUri is not set.")), credential);
        _container = service.GetBlobContainerClient(options.Container);
    }

    public async Task WriteAsync(string path, Stream content, string contentType, CancellationToken ct)
    {
        // IfNoneMatch "*": refuse to overwrite an existing blob.
        await _container.GetBlobClient(path).UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            Conditions = new BlobRequestConditions { IfNoneMatch = Azure.ETag.All },
        }, ct);
    }

    public async Task<Stream> OpenReadAsync(string path, CancellationToken ct) =>
        await _container.GetBlobClient(path).OpenReadAsync(cancellationToken: ct);
}

public sealed class LocalFileStore(StorageOptions options) : IFileStore
{
    private readonly string _root = Path.GetFullPath(options.LocalPath);

    public async Task WriteAsync(string path, Stream content, string contentType, CancellationToken ct)
    {
        var full = Resolve(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var file = new FileStream(full, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(file, ct);
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct) =>
        Task.FromResult<Stream>(new FileStream(Resolve(path), FileMode.Open, FileAccess.Read, FileShare.Read));

    private string Resolve(string path)
    {
        var full = Path.GetFullPath(Path.Combine(_root, path));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Path escapes the storage root.", nameof(path));
        }
        return full;
    }
}
