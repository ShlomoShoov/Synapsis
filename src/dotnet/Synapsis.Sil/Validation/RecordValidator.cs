using System;
using System.Collections.Generic;
using System.Text.Json;
using Synapsis.Sil.Reading;
using Synapsis.Sil.Records;
using Synapsis.Sil.Registry;
using Synapsis.Sil.Validation.Envelope;
using Synapsis.Sil.Validation.Rules;

namespace Synapsis.Sil.Validation;

/// <summary>
/// Checks one record against the record rules, in their fixed order: rule 1 (kind), rule 2 (structure), then the
/// record is read into its typed class, then rules 3 to 12. The first rule that fails decides the record's error,
/// so the order of <see cref="Rules"/> is part of the contract (tests/conformance).
/// </summary>
public static class RecordValidator
{
    /// <summary>Rules 3 to 12, in the order they run.</summary>
    private static readonly IReadOnlyList<IRecordRule> Rules = new IRecordRule[]
    {
        new SourceRule(),          // rule 3
        new IdentityRule(),        // rule 4
        new TypeRule(),            // rule 5
        new ValidTimeRule(),       // rule 6
        new KnowledgeTimeRule(),   // rule 7
        new PlaceRule(),           // rule 8
        new MeasureRule(),         // rule 9
        new TextRule(),            // rule 10
        new AttributeRule(),       // rule 11
        new ConclusionRule(),      // rule 12
    };

    /// <summary>
    /// Checks one raw record. Returns its error, or null when it passes every record rule; then
    /// <paramref name="record"/> holds the typed record.
    /// </summary>
    public static RecordError? Check(JsonElement raw, RegistrySnapshot registry, DateTime receivedAt, out SilRecord? record)
    {
        record = null;
        RecordError? error = KindCheck.Check(raw, out RecordKind kind);
        if (error != null)
        {
            return error;
        }
        error = EnvelopeSchema.Check(kind, raw);
        if (error != null)
        {
            return error;
        }
        error = ReadTyped(raw, out SilRecord? typed);
        if (error != null)
        {
            return error;
        }
        error = RunRules(new RecordContext(typed!, registry, receivedAt));
        if (error == null)
        {
            record = typed;
        }
        return error;
    }

    /// <summary>Runs rules 3 to 12 and returns the first failure, or null.</summary>
    private static RecordError? RunRules(RecordContext context)
    {
        foreach (IRecordRule rule in Rules)
        {
            RecordError? error = rule.Check(context);
            if (error != null)
            {
                return error;
            }
        }
        return null;
    }

    /// <summary>
    /// Reads the record into its typed class. A value the schema allows but the C# type cannot hold (a reliability
    /// written as 95.0) is a structure problem: E-SCHEMA.
    /// </summary>
    private static RecordError? ReadTyped(JsonElement raw, out SilRecord? typed)
    {
        typed = null;
        try
        {
            typed = SilReader.ReadRecord(raw);
            return null;
        }
        catch (JsonException problem)
        {
            return new RecordError(ErrorCodes.Schema, string.Empty, problem.Message);
        }
    }
}
