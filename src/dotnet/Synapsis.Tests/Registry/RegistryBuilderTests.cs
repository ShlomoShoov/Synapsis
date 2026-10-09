using System;
using System.Collections.Generic;
using Synapsis.Sil.Records;
using Synapsis.Sil.Registry;
using Synapsis.Sil.Vocabulary;
using Synapsis.Sil.Vocabulary.Types;
using Synapsis.Tests.Support;
using Xunit;

namespace Synapsis.Tests.Registry;

/// <summary>Checks the registry built from the repository's real packages.</summary>
public class RegistryBuilderTests
{
    /// <summary>The closed lists come from core.</summary>
    [Fact]
    public void The_closed_lists_come_from_core()
    {
        RegistrySnapshot registry = TestRegistry.All;
        Assert.Equal(8, registry.Precisions.Count);
        Assert.Contains("proxy_publication_lag", registry.KnownAtBases);
        Assert.Contains("A1", registry.ReliabilityBases);
        Assert.Contains("agent", registry.MethodBy);
        Assert.Contains("US", registry.Countries);
    }

    /// <summary>Each source and namespace knows its owning package.</summary>
    [Fact]
    public void Sources_and_namespaces_know_their_package()
    {
        RegistrySnapshot registry = TestRegistry.All;
        Assert.Equal(29, registry.Sources.Count);
        Assert.Equal("kev", registry.SourcePackages["src:cisa:kev"]);
        Assert.Equal("kev", registry.RecordNamespaces["kev"]);
        Assert.Equal("kev", registry.EntityNamespaces["kev-product"]);
        Assert.Equal("Vulnerability", registry.SharedNamespaces["cve"].EntityType);
    }

    /// <summary>Types, measures and logical spaces are found by code.</summary>
    [Fact]
    public void Types_measures_and_spaces_are_found_by_code()
    {
        RegistrySnapshot registry = TestRegistry.All;
        Assert.Equal(37, registry.EventTypes.Count);
        Assert.True(registry.HasType(RecordKind.Event, "CYB.VULN.KNOWN_EXPLOITED"));
        Assert.True(registry.HasType(RecordKind.Relation, "PRODUCT_OF"));
        Assert.False(registry.HasType(RecordKind.Entity, "CYB.VULN.KNOWN_EXPLOITED"));
        Assert.True(registry.Measures["equity"].Signed);
        Assert.True(registry.LogicalSpaces["cyber.product_class"].Closed);
        Assert.True(registry.AttributesOf(RecordKind.Event, "CYB.VULN.KNOWN_EXPLOITED").ContainsKey("cwes"));
        Assert.Empty(registry.AttributesOf(RecordKind.Conclusion, "classification"));
    }

    /// <summary>Every pattern is compiled once and matches as declared.</summary>
    [Fact]
    public void Patterns_are_compiled()
    {
        RegistrySnapshot registry = TestRegistry.All;
        Assert.Matches(registry.Patterns["^CVE-[0-9]{4}-[0-9]{4,7}$"], "CVE-2023-34362");
    }

    /// <summary>Two packages defining the same code stop the build.</summary>
    [Fact]
    public void A_code_defined_twice_is_refused()
    {
        SchemaPackage first = new SchemaPackage { Name = "a", EventTypes = new List<EventTypeDefinition> { new EventTypeDefinition { Code = "X.ONE" } } };
        SchemaPackage second = new SchemaPackage { Name = "b", EventTypes = new List<EventTypeDefinition> { new EventTypeDefinition { Code = "X.ONE" } } };
        Assert.Throws<InvalidOperationException>(() => RegistryBuilder.Build(new List<SchemaPackage> { first, second }));
    }
}
