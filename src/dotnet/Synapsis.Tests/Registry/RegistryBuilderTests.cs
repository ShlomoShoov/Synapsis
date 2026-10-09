using System.Collections.Generic;
using Synapsis.Sil;
using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Models.Vocabulary;
using Synapsis.Sil.Models.Vocabulary.Types;
using Synapsis.Sil.Registry.Abstractions;
using Synapsis.Tests.Support;
using Xunit;

namespace Synapsis.Tests.Registry;

/// <summary>Checks the registry built from the repository's real packages, through its questions.</summary>
public class RegistryBuilderTests
{
    /// <summary>The closed lists come from core.</summary>
    [Fact]
    public void The_closed_lists_come_from_core()
    {
        IRegistry registry = TestRegistry.All;
        Assert.True(registry.IsPrecision("quarter"));
        Assert.False(registry.IsPrecision("decade"));
        Assert.True(registry.IsKnownAtBasis("proxy_publication_lag"));
        Assert.True(registry.IsReliabilityBasis("A1"));
        Assert.True(registry.IsMethodBy("agent"));
        Assert.True(registry.IsCountry("US"));
    }

    /// <summary>Each source and namespace knows its owning package.</summary>
    [Fact]
    public void Sources_and_namespaces_know_their_package()
    {
        IRegistry registry = TestRegistry.All;
        Assert.Equal(95, registry.FindSource("src:cisa:kev")!.Reliability);
        Assert.Equal("kev", registry.PackageOfSource("src:cisa:kev"));
        Assert.Equal("kev", registry.OwnerOfRecordNamespace("kev"));
        Assert.Equal("kev", registry.OwnerOfEntityNamespace("kev-product"));
        Assert.Null(registry.OwnerOfRecordNamespace("nobody"));
        Assert.Equal("Vulnerability", registry.FindSharedNamespace("cve")!.EntityType);
    }

    /// <summary>Types, measures and logical spaces are found by code.</summary>
    [Fact]
    public void Types_measures_and_spaces_are_found_by_code()
    {
        IRegistry registry = TestRegistry.All;
        Assert.True(registry.HasType(RecordKind.Event, "CYB.VULN.KNOWN_EXPLOITED"));
        Assert.True(registry.HasType(RecordKind.Relation, "PRODUCT_OF"));
        Assert.False(registry.HasType(RecordKind.Entity, "CYB.VULN.KNOWN_EXPLOITED"));
        Assert.True(registry.FindMeasure("equity")!.Signed);
        Assert.True(registry.FindLogicalSpace("cyber.product_class")!.Closed);
        Assert.True(registry.AttributesOf(RecordKind.Event, "CYB.VULN.KNOWN_EXPLOITED").ContainsKey("cwes"));
        Assert.Empty(registry.AttributesOf(RecordKind.Conclusion, "classification"));
    }

    /// <summary>Patterns the vocabulary declares are applied.</summary>
    [Fact]
    public void Patterns_are_applied()
    {
        Assert.True(TestRegistry.All.Matches("^CVE-[0-9]{4}-[0-9]{4,7}$", "CVE-2023-34362"));
        Assert.False(TestRegistry.All.Matches("^CVE-[0-9]{4}-[0-9]{4,7}$", "CVE-99"));
    }

    /// <summary>Two packages defining the same code are refused with E-SCHEMA-OWNERSHIP.</summary>
    [Fact]
    public void A_code_defined_twice_is_refused()
    {
        SchemaPackage first = new SchemaPackage { Name = "a", EventTypes = new List<EventTypeDefinition> { new EventTypeDefinition { Code = "X.ONE" } } };
        SchemaPackage second = new SchemaPackage { Name = "b", EventTypes = new List<EventTypeDefinition> { new EventTypeDefinition { Code = "X.ONE" } } };
        Result<IRegistry> result = SilComponents.CreateRegistryBuilder().Build(new List<SchemaPackage> { first, second });
        Assert.Equal(ErrorCodes.SchemaOwnership, result.Problem!.Code);
    }
}
