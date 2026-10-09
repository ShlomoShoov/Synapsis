namespace Synapsis.Sil.Packages;

/// <summary>
/// Whether records of an event type carry text. Fixed by the package format, so it is a C# enum.
/// In YAML: none (the default), optional, required.
/// </summary>
public enum TextPolicy
{
    /// <summary>The records carry no text (measurement-only types keep the search index lean).</summary>
    None,

    /// <summary>The records may carry text.</summary>
    Optional,

    /// <summary>Every record carries a title or a body (search depends on it).</summary>
    Required,
}
