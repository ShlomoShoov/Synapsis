using Synapsis.Sil.Services.Fingerprinting;
using Synapsis.Sil.Orchestrators;
using Synapsis.Sil.Services.Rules.Package;
using Synapsis.Sil.Services.Reading;
using Synapsis.Sil.Services.Registry;
using Synapsis.Sil.Services.Rules.Batch;
using Synapsis.Sil.Services.Rules.Envelope;
using Synapsis.Sil.Services.Rules.Record;
using Synapsis.Sil.Services.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace Synapsis.Sil;

/// <summary>
/// Registers the record language for dependency injection: the readers, the registry builder, the fingerprinter,
/// the validators, the version planner and every rule. The rules are registered in the order they run, because the
/// order is part of the contract: the first rule a record breaks decides its error code.
/// The caller also registers an <see cref="IReferenceResolver"/> (what is already stored: MongoDB in the API, a fake in tests).
/// </summary>
public static class SilRegistration
{
    /// <summary>Adds every component of the record language as a singleton (they keep no state between calls).</summary>
    public static IServiceCollection AddSynapsisSil(this IServiceCollection services)
    {
        services.AddSingleton<IBundleReader, BundleReader>();
        services.AddSingleton<IRecordReader, RecordReader>();
        services.AddSingleton<IPackageReader, PackageReader>();
        services.AddSingleton<IRegistryBuilder, RegistryBuilder>();
        services.AddSingleton<IFingerprinter, Fingerprinter>();
        services.AddSingleton<IVersionPlanner, VersionPlanner>();
        services.AddSingleton<IRecordValidator, RecordValidator>();
        services.AddSingleton<IBatchValidator, BatchValidator>();
        services.AddSingleton<IPackageValidator, PackageValidator>();
        AddEnvelopeRules(services);
        AddRecordRules(services);
        AddBatchRules(services);
        AddPackageRules(services);
        return services;
    }

    /// <summary>Rules 1 and 2, on the raw JSON.</summary>
    private static void AddEnvelopeRules(IServiceCollection services)
    {
        services.AddSingleton<IEnvelopeRule, KindRule>();          // rule 1
        services.AddSingleton<IEnvelopeRule, StructureRule>();     // rule 2
    }

    /// <summary>Rules 3 to 12, on the typed record.</summary>
    private static void AddRecordRules(IServiceCollection services)
    {
        services.AddSingleton<IRecordRule, SourceRule>();          // rule 3
        services.AddSingleton<IRecordRule, IdentityRule>();        // rule 4
        services.AddSingleton<IRecordRule, TypeRule>();            // rule 5
        services.AddSingleton<IRecordRule, ValidTimeRule>();       // rule 6
        services.AddSingleton<IRecordRule, KnowledgeTimeRule>();   // rule 7
        services.AddSingleton<IRecordRule, PlaceRule>();           // rule 8
        services.AddSingleton<IRecordRule, MeasureRule>();         // rule 9
        services.AddSingleton<IRecordRule, TextRule>();            // rule 10
        services.AddSingleton<IRecordRule, AttributeRule>();       // rule 11
        services.AddSingleton<IRecordRule, ConclusionRule>();      // rule 12
    }

    /// <summary>Rule 13, across the batch: duplicates first, then references.</summary>
    private static void AddBatchRules(IServiceCollection services)
    {
        services.AddSingleton<IBatchRule, DuplicateRule>();
        services.AddSingleton<IBatchRule, ReferenceRule>();
    }

    /// <summary>The package rules: format, name, compatibility with the stored version, references. Ownership comes last, when the vocabulary is built.</summary>
    private static void AddPackageRules(IServiceCollection services)
    {
        services.AddSingleton<IPackageRule, FormatRule>();
        services.AddSingleton<IPackageRule, NameRule>();
        services.AddSingleton<IPackageRule, CompatibilityRule>();
        services.AddSingleton<IPackageRule, PackageReferenceRule>();
    }
}
