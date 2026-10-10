using System.Collections.Generic;
using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Vocabulary.Attributes;
using Synapsis.Sil.Models.Vocabulary.Ownership;
using Synapsis.Sil.Models.Vocabulary.Sources;
using Synapsis.Sil.Models.Vocabulary.Terms;
using Synapsis.Sil.Models.Vocabulary.Types;
namespace Synapsis.Sil.Services.Registry;

/// <summary>
/// The vocabulary of all registered schema packages, answering the questions the record rules ask: is this source
/// declared, who owns this id namespace, which measures does this event type allow. It never changes once built, so
/// every record of a batch is checked against the same vocabulary.
/// </summary>
public interface IRegistry
{
    /// <summary>True when the precision is in core's list (second … year).</summary>
    bool IsPrecision(string precision);

    /// <summary>True when the known_at basis is in core's list (published, observed, …).</summary>
    bool IsKnownAtBasis(string basis);

    /// <summary>True when the reliability basis is in core's list (A1 … F6, measured, analyst, default, model).</summary>
    bool IsReliabilityBasis(string basis);

    /// <summary>True when the producer kind of a conclusion is in core's list (human, agent, system).</summary>
    bool IsMethodBy(string methodBy);

    /// <summary>True when the code is an ISO 3166-1 alpha-2 country code in core's list.</summary>
    bool IsCountry(string countryCode);

    /// <summary>The source with this id, or null when no package declares it.</summary>
    SourceDefinition? FindSource(string sourceId);

    /// <summary>The name of the package that declares this source, or null.</summary>
    string? PackageOfSource(string sourceId);

    /// <summary>The package that owns this namespace of event, relation and conclusion ids, or null.</summary>
    string? OwnerOfRecordNamespace(string space);

    /// <summary>The package that owns this namespace of entity ids, or null.</summary>
    string? OwnerOfEntityNamespace(string space);

    /// <summary>The shared entity namespace with this name (cve, country, …), or null.</summary>
    SharedNamespace? FindSharedNamespace(string space);

    /// <summary>True when a type with this code is registered for this kind of record.</summary>
    bool HasType(RecordKind kind, string code);

    /// <summary>The event type with this code, or null.</summary>
    EventTypeDefinition? FindEventType(string code);

    /// <summary>The relation type with this code, or null.</summary>
    RelationTypeDefinition? FindRelationType(string code);

    /// <summary>The attributes a type declares, by name; empty for conclusion types and unknown types.</summary>
    IReadOnlyDictionary<string, AttributeDefinition> AttributesOf(RecordKind kind, string code);

    /// <summary>The measure with this name, or null.</summary>
    MeasureDefinition? FindMeasure(string name);

    /// <summary>The logical space with this name, or null.</summary>
    LogicalSpaceDefinition? FindLogicalSpace(string space);

    /// <summary>True when the text matches a pattern the vocabulary declares (an attribute pattern, a key pattern).</summary>
    bool Matches(string pattern, string text);
}
