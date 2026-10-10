namespace Synapsis.Sil.Models.Records.Parts;

/// <summary>
/// Who or what produced a conclusion, and how. Example: { "by": "agent", "name": "teacher:claude-sonnet-5-5", "version": "r3" }.
/// </summary>
public sealed class Method
{
    /// <summary>The kind of producer: human, agent or system (the list lives in the core package).</summary>
    public required string By { get; init; }

    /// <summary>The method's name, for example a model or an algorithm.</summary>
    public required string Name { get; init; }

    /// <summary>The method's version, so findings from different versions can be told apart.</summary>
    public required string Version { get; init; }
}
