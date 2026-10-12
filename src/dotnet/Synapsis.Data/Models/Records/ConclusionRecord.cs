using System.Collections.Generic;

namespace Synapsis.Data.Models.Records;

/// <summary>A conclusion: something a person, a model or the engine concluded about other records.</summary>
public sealed class ConclusionRecord : SilRecord
{
    /// <summary>What it is about: record ids or subject keys.</summary>
    public List<string> About { get; set; } = new List<string>();

    /// <summary>What it says.</summary>
    public Statement? Statement { get; set; }

    /// <summary>The ids of the records it is based on.</summary>
    public List<string> Premises { get; set; } = new List<string>();

    /// <summary>Who or what concluded it.</summary>
    public Method? Method { get; set; }

    /// <summary>How sure, 0 to 100.</summary>
    public double? Confidence { get; set; }
}
