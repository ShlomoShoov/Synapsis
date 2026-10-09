using System;
using System.Text.Json;
using Synapsis.Sil.Registry.Abstractions;

namespace Synapsis.Sil.Validation.Models;

/// <summary>What validating one record needs: the raw record, the vocabulary, and when it arrived.</summary>
public sealed class RecordValidationRequest
{
    /// <summary>Creates a request.</summary>
    public RecordValidationRequest(JsonElement raw, IRegistry registry, DateTime receivedAt)
    {
        Raw = raw;
        Registry = registry;
        ReceivedAt = receivedAt;
    }

    /// <summary>The record as the producer sent it.</summary>
    public JsonElement Raw { get; }

    /// <summary>The vocabulary to check it against.</summary>
    public IRegistry Registry { get; }

    /// <summary>When the server received it (UTC).</summary>
    public DateTime ReceivedAt { get; }
}
