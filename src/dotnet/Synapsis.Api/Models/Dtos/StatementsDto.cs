using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Synapsis.Api.Models.Dtos;

/// <summary>
/// The answer to GET /records/{id}: for each source, the record as known at the date asked, with its "version" and
/// "_ingest" (batch_id, producer, received_at) added beside its fields.
/// </summary>
public sealed class StatementsDto
{
    /// <summary>The record id asked for.</summary>
    public required string Id { get; init; }

    /// <summary>The as_of asked for, as given; left out when the latest was asked.</summary>
    public string? AsOf { get; init; }

    /// <summary>One statement per source.</summary>
    public required List<JsonObject> Statements { get; init; }
}
