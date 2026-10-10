using System;
using System.Collections.Generic;
using System.Text.Json;
using Synapsis.Sil.Services.Registry;
namespace Synapsis.Sil.Models.Validation;

/// <summary>What batch validation needs: the raw records, the vocabulary to check them against, and when they arrived.</summary>
public sealed class BatchValidationRequest
{
    /// <summary>Creates a request.</summary>
    public BatchValidationRequest(IReadOnlyList<JsonElement> records, IRegistry registry, DateTime receivedAt)
    {
        Records = records;
        Registry = registry;
        ReceivedAt = receivedAt;
    }

    /// <summary>The records as the producer sent them, in batch order.</summary>
    public IReadOnlyList<JsonElement> Records { get; }

    /// <summary>The vocabulary, the same for every record of the batch.</summary>
    public IRegistry Registry { get; }

    /// <summary>When the server received the batch (UTC): the limit for known_at.</summary>
    public DateTime ReceivedAt { get; }
}
