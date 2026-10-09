using System;

namespace Synapsis.Storage.Records.Models.Requests;

/// <summary>Which statements of a record id to read: as known by a date (or the latest), from one source (or all).</summary>
public sealed class StatementsAsOfRequest
{
    /// <summary>Creates a request.</summary>
    public StatementsAsOfRequest(string id, DateTime? asOf, string? source)
    {
        Id = id;
        AsOf = asOf;
        Source = source;
    }

    /// <summary>The record id.</summary>
    public string Id { get; }

    /// <summary>Only versions known by this moment (UTC); null for the latest.</summary>
    public DateTime? AsOf { get; }

    /// <summary>Only this source's statement; null for every source.</summary>
    public string? Source { get; }
}
