using System;
using System.Collections.Generic;

namespace Synapsis.Storage.Models;

/// <summary>
/// One line of the ingestion log: what one batch brought from one source. A batch with records from two sources gives
/// two entries. Stored as a document in the Elasticsearch index "synapsis-ingest-log" and returned as is by
/// GET /observe/ingest, so the UI sees exactly what is stored.
/// </summary>
public sealed class IngestLogEntry
{
    /// <summary>When the API received the batch (UTC).</summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>The batch id the producer gave.</summary>
    public required string BatchId { get; init; }

    /// <summary>Who sent the batch, such as "feeder:kev".</summary>
    public required string Producer { get; init; }

    /// <summary>The source the records name, such as "src:cisa:kev"; "unknown" when a refused record had no readable source.</summary>
    public required string Source { get; init; }

    /// <summary>How many records of this source the batch held.</summary>
    public int Received { get; set; }

    /// <summary>Records stored for the first time (version 1).</summary>
    public int New { get; set; }

    /// <summary>Records stored as a new version of what was there.</summary>
    public int Changed { get; set; }

    /// <summary>Records identical to what was already stored: nothing written.</summary>
    public int Unchanged { get; set; }

    /// <summary>Exact copies inside the same batch, dropped.</summary>
    public int Duplicates { get; set; }

    /// <summary>Records refused with an error code.</summary>
    public int Rejected { get; set; }

    /// <summary>How many records each error code refused.</summary>
    public List<RejectionCount> RejectionCounts { get; init; } = new List<RejectionCount>();

    /// <summary>The first refused records in detail (at most 50 per entry, to keep the log small).</summary>
    public List<LoggedRejection> Rejections { get; init; } = new List<LoggedRejection>();

    /// <summary>How long the API took to handle the whole batch, in milliseconds.</summary>
    public long DurationMs { get; set; }
}
