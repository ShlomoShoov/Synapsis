namespace Synapsis.Sil.Models.Domain.Vocabulary.Attributes;

/// <summary>
/// The value type of a declared attribute. Fixed by the package format, so it is a C# enum.
/// In YAML: string, integer, boolean, date, string_list.
/// </summary>
public enum AttributeType
{
    /// <summary>A text value.</summary>
    String,

    /// <summary>A whole number.</summary>
    Integer,

    /// <summary>true or false.</summary>
    Boolean,

    /// <summary>A calendar date written as 2024-01-31.</summary>
    Date,

    /// <summary>A list of text values.</summary>
    StringList,
}
