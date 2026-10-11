namespace Synapsis.Api.Models.Records;

/// <summary>A relation between two records, such as "this bank acquired that bank".</summary>
public sealed class RelationRecord : SilRecord
{
    /// <summary>The id of the record it goes from (required).</summary>
    public string? From { get; set; }

    /// <summary>The id of the record it goes to (required).</summary>
    public string? To { get; set; }
}
