using System;
using System.Collections.Generic;
using Synapsis.Sil.Validation.Models.Domain;
using Synapsis.Sil.Versioning.Models.Domain;

namespace Synapsis.Sil.Versioning.Models.Requests;

/// <summary>What planning one record needs: the record, the stored versions of its statement, and the receive time.</summary>
public sealed class VersionPlanRequest
{
    /// <summary>Creates a request.</summary>
    public VersionPlanRequest(CheckedRecord record, IReadOnlyList<StoredVersion> storedVersions, DateTime receivedAt)
    {
        Record = record;
        StoredVersions = storedVersions;
        ReceivedAt = receivedAt;
    }

    /// <summary>The accepted record, with its fingerprint.</summary>
    public CheckedRecord Record { get; }

    /// <summary>Every stored version of the record's statement, in any order; empty when there is none.</summary>
    public IReadOnlyList<StoredVersion> StoredVersions { get; }

    /// <summary>When the server received the record (UTC).</summary>
    public DateTime ReceivedAt { get; }
}
