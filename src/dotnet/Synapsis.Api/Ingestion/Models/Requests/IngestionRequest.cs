namespace Synapsis.Api.Ingestion.Models.Requests;

/// <summary>A batch to ingest: the request body as text, and whether to stop before keeping anything.</summary>
public sealed class IngestionRequest
{
    /// <summary>Creates a request.</summary>
    public IngestionRequest(string body, bool dryRun)
    {
        Body = body;
        DryRun = dryRun;
    }

    /// <summary>The body as sent (already unpacked when it was gzip).</summary>
    public string Body { get; }

    /// <summary>True to validate and plan only: the receipt says what would be stored.</summary>
    public bool DryRun { get; }
}
