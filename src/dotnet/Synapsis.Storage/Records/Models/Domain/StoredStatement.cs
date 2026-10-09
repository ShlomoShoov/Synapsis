using System;
using System.Text.Json;

namespace Synapsis.Storage.Records.Models.Domain;

/// <summary>One stored version of a statement, as a read returns it: the record and where it came from.</summary>
public sealed class StoredStatement
{
    /// <summary>The record as stored.</summary>
    public required JsonElement Record { get; init; }

    /// <summary>Its version number.</summary>
    public required int Version { get; init; }

    /// <summary>The batch that brought it.</summary>
    public required string BatchId { get; init; }

    /// <summary>The producer that sent the batch.</summary>
    public required string Producer { get; init; }

    /// <summary>When the server received it (UTC).</summary>
    public required DateTime ReceivedAt { get; init; }
}
