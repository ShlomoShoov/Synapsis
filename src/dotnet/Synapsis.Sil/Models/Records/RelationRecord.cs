using Synapsis.Sil.Models.Records.Parts;
namespace Synapsis.Sil.Models.Records;

/// <summary>
/// A relation: a lasting link from one entity to another, such as "this product belongs to this vendor" (PRODUCT_OF)
/// or "this bank acquired that failed bank" (ACQUIRED).
/// </summary>
public sealed class RelationRecord : SilRecord
{
    /// <summary>The id of the entity the link starts from.</summary>
    public required string From { get; init; }

    /// <summary>The id of the entity the link points to.</summary>
    public required string To { get; init; }

    /// <summary>Optional: when the link held, for example the date of an acquisition.</summary>
    public ValidTime? Time { get; init; }
}
