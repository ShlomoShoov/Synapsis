namespace Synapsis.Sil.Models.Versioning;

/// <summary>What an accepted record means for the store.</summary>
public enum VersionOutcome
{
    /// <summary>Nothing is stored for this statement yet: it becomes version 1.</summary>
    New,

    /// <summary>The store already holds this content: nothing is written.</summary>
    Unchanged,

    /// <summary>The content changed: it becomes the next version.</summary>
    NewVersion,
}
