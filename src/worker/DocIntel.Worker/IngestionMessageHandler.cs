using System.Text.Json;

namespace DocIntel.Worker;

/// <summary>An ingestion job: which document version to extract and index (M3 fills in the work).</summary>
public sealed record IngestionJob(Guid DocumentId, string Version);

/// <summary>
/// Turns a queue message into an ingestion job. A message that cannot be read throws,
/// so Service Bus retries it and, after the queue's max delivery count, dead-letters it.
/// </summary>
public sealed class IngestionMessageHandler(ILogger<IngestionMessageHandler> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public IngestionJob Handle(string body)
    {
        IngestionJob? job;
        try
        {
            job = JsonSerializer.Deserialize<IngestionJob>(body, Json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Ingestion message is not valid JSON.", ex);
        }

        if (job is null || job.DocumentId == Guid.Empty || string.IsNullOrWhiteSpace(job.Version))
        {
            throw new InvalidDataException("Ingestion message needs a documentId and a version.");
        }

        logger.LogInformation("Ingestion job received for document {DocumentId} version {Version}", job.DocumentId, job.Version);
        return job;
    }
}
