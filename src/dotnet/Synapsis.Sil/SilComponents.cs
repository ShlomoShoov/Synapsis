using System.Collections.Generic;
using Synapsis.Sil.Fingerprinting.Abstractions;
using Synapsis.Sil.Fingerprinting.Services.Domain;
using Synapsis.Sil.Reading.Abstractions;
using Synapsis.Sil.Reading.Services.Readers;
using Synapsis.Sil.Registry.Abstractions;
using Synapsis.Sil.Registry.Services.Domain;
using Synapsis.Sil.Validation.Abstractions;
using Synapsis.Sil.Validation.Services.BatchRules;
using Synapsis.Sil.Validation.Services.EnvelopeRules;
using Synapsis.Sil.Validation.Services.Orchestrators;
using Synapsis.Sil.Validation.Services.RecordRules;

namespace Synapsis.Sil;

/// <summary>
/// The one place where the components of the record language are wired together: each method returns a component
/// as its abstraction, built from the default implementations. The order of the validation rules lives here too,
/// because it is part of the contract (the first rule a record breaks decides its error code).
/// Read this file to see how the components connect; read the Abstractions folders to see what each one does.
/// </summary>
public static class SilComponents
{
    /// <summary>Reads request bodies into bundles.</summary>
    public static IBundleReader CreateBundleReader()
    {
        return new BundleReader();
    }

    /// <summary>Reads one raw record into the typed class of its kind.</summary>
    public static IRecordReader CreateRecordReader()
    {
        return new RecordReader();
    }

    /// <summary>Reads schema packages from YAML or JSON.</summary>
    public static IPackageReader CreatePackageReader()
    {
        return new PackageReader();
    }

    /// <summary>Builds the registry (the vocabulary) from schema packages.</summary>
    public static IRegistryBuilder CreateRegistryBuilder()
    {
        return new RegistryBuilder();
    }

    /// <summary>Fingerprints a record's content.</summary>
    public static IFingerprinter CreateFingerprinter()
    {
        return new Fingerprinter();
    }

    /// <summary>Validates one record on its own: rules 1 to 12, in order.</summary>
    public static IRecordValidator CreateRecordValidator()
    {
        return new RecordValidator(EnvelopeRulesInOrder(), CreateRecordReader(), RecordRulesInOrder());
    }

    /// <summary>
    /// Validates a whole batch: every record on its own, then the batch rules. <paramref name="storedRecords"/> answers
    /// what is already stored (MongoDB in the API; a fake in tests).
    /// </summary>
    public static IBatchValidator CreateBatchValidator(IReferenceResolver storedRecords)
    {
        return new BatchValidator(CreateRecordValidator(), CreateFingerprinter(), BatchRulesInOrder(storedRecords));
    }

    /// <summary>Rules 1 and 2, on the raw JSON.</summary>
    private static IReadOnlyList<IEnvelopeRule> EnvelopeRulesInOrder()
    {
        return new IEnvelopeRule[]
        {
            new KindRule(),            // rule 1
            new StructureRule(),       // rule 2
        };
    }

    /// <summary>Rules 3 to 12, on the typed record.</summary>
    private static IReadOnlyList<IRecordRule> RecordRulesInOrder()
    {
        return new IRecordRule[]
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
    }

    /// <summary>Rule 13, across the batch: duplicates first, then references.</summary>
    private static IReadOnlyList<IBatchRule> BatchRulesInOrder(IReferenceResolver storedRecords)
    {
        return new IBatchRule[]
        {
            new DuplicateRule(),
            new ReferenceRule(storedRecords),
        };
    }
}
