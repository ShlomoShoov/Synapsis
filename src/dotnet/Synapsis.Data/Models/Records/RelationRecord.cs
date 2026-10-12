namespace Synapsis.Data.Models.Records;

/// <summary>A relation between two records, such as "this bank acquired that bank".</summary>
public sealed class RelationRecord : SilRecord
{
    /// <summary>The id of the record it goes from.</summary>
    public string? From { get; set; }

    /// <summary>The id of the record it goes to.</summary>
    public string? To { get; set; }

    /// <summary>When the relation held, when the source says.</summary>
    public ValidTime? Time { get; set; }
}
