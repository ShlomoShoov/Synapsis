using System.Collections.Generic;
using Synapsis.Storage.Models;

namespace Synapsis.Api.Models;

/// <summary>The answer of the change-detection step: what to store, and what is already stored as it is.</summary>
public sealed class ChangeResult
{
    /// <summary>New records (state 1) and changed ones (the next state): to store.</summary>
    public List<StoredRecord> ToSave { get; set; } = new List<StoredRecord>();

    /// <summary>Records whose latest stored state has the same content: nothing to store.</summary>
    public List<StoredRecord> Unchanged { get; set; } = new List<StoredRecord>();
}
