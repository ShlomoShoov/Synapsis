using System;
using System.Text.Json;
using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Versioning;
namespace Synapsis.Storage.Models;

/// <summary>One version of a statement, ready to be stored: the record as it will be kept, and where it came from.</summary>
public sealed class RecordVersion
{
    /// <summary>The statement: record id and source.</summary>
    public required StatementKey Key { get; init; }

    /// <summary>The record's kind.</summary>
    public required RecordKind Kind { get; init; }

    /// <summary>The version number: 1, 2, ...</summary>
    public required int Version { get; init; }

    /// <summary>When this version became known (UTC).</summary>
    public required DateTime KnownAt { get; init; }

    /// <summary>The fingerprint of its content.</summary>
    public required string Fingerprint { get; init; }

    /// <summary>The record exactly as it will be stored.</summary>
    public required JsonElement Record { get; init; }

    /// <summary>The batch that brought it.</summary>
    public required string BatchId { get; init; }

    /// <summary>The producer that sent the batch.</summary>
    public required string Producer { get; init; }

    /// <summary>When the server received the batch (UTC).</summary>
    public required DateTime ReceivedAt { get; init; }
}
