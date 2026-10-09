using System.Collections.Generic;
using System.IO;
using Synapsis.Sil.Vocabulary;
using Synapsis.Sil.Vocabulary.Attributes;
using Synapsis.Sil.Vocabulary.Ownership;
using Synapsis.Sil.Vocabulary.Sources;
using Synapsis.Sil.Vocabulary.Terms;
using Synapsis.Sil.Vocabulary.Types;
using Synapsis.Sil.Reading;
using Xunit;
using YamlDotNet.Core;

namespace Synapsis.Tests;

/// <summary>
/// Checks that the real schema packages (feeders/packages/*.yaml and schemas/seed/core.yaml) read into the typed
/// package classes, with nothing lost or misread.
/// </summary>
public class PackageReaderTests
{
    /// <summary>The file of every package in the repository, one test case each.</summary>
    public static IEnumerable<object[]> AllPackageFiles()
    {
        List<object[]> files = new List<object[]>();
        files.Add(new object[] { "schemas/seed/core.yaml" });
        foreach (string path in Directory.GetFiles(RepositoryFiles.PathOf("feeders/packages"), "*.yaml"))
        {
            files.Add(new object[] { "feeders/packages/" + Path.GetFileName(path) });
        }
        return files;
    }

    /// <summary>Every package reads, and its name matches its file name.</summary>
    [Theory]
    [MemberData(nameof(AllPackageFiles))]
    public void Every_package_reads_and_is_named_like_its_file(string file)
    {
        SchemaPackage package = PackageReader.Read(RepositoryFiles.ReadText(file));
        Assert.Equal(Path.GetFileNameWithoutExtension(file), package.Name);
        Assert.False(string.IsNullOrEmpty(package.Title));
    }

    /// <summary>KEV: its source, its event type (place, roles, text) and its relation type are read in full.</summary>
    [Fact]
    public void The_kev_package_reads_in_full()
    {
        SchemaPackage kev = Read("feeders/packages/kev.yaml");
        Assert.Equal(new List<string> { "kev" }, kev.Namespaces.Records);
        Assert.Equal(new List<string> { "kev-vendor", "kev-product" }, kev.Namespaces.Entities);

        SourceDefinition source = kev.Sources[0];
        Assert.Equal("src:cisa:kev", source.Id);
        Assert.Equal(95, source.Reliability);
        Assert.Equal("A1", source.ReliabilityBasis);
        Assert.Equal(1.0, source.PublicationLagDays);

        EventTypeDefinition exploited = kev.EventTypes[0];
        Assert.Equal("CYB.VULN.KNOWN_EXPLOITED", exploited.Code);
        Assert.Equal(new List<PlaceForm> { PlaceForm.Logical }, exploited.Place);
        Assert.Equal(new List<string> { "Vulnerability" }, exploited.Roles["subject"]);
        Assert.Equal(TextPolicy.Required, exploited.Text);
        Assert.Equal(AttributeType.StringList, exploited.Attributes["cwes"].Type);
        Assert.Equal(new List<string> { "Known", "Unknown" }, exploited.Attributes["known_ransomware_campaign_use"].AllowedValues);

        RelationTypeDefinition productOf = kev.RelationTypes[0];
        Assert.Equal(new List<string> { "Product" }, productOf.From);
        Assert.Equal(new List<string> { "Vendor" }, productOf.To);
        Assert.Equal(2, kev.LogicalSpaces[0].MaxDepth);
    }

    /// <summary>FDIC: signed measures and attributes shared through a YAML anchor are read correctly.</summary>
    [Fact]
    public void The_fdic_package_reads_signed_measures_and_shared_attributes()
    {
        SchemaPackage fdic = Read("feeders/packages/fdic.yaml");
        MeasureDefinition equity = FindMeasure(fdic, "equity");
        Assert.Equal("usd_k", equity.Unit);
        Assert.True(equity.Signed);
        MeasureDefinition assets = FindMeasure(fdic, "assets");
        Assert.False(assets.Signed);
        Assert.Equal(0.0, assets.Min);

        EventTypeDefinition assistance = FindEventType(fdic, "FIN.BANK.ASSISTANCE");
        Assert.True(assistance.Attributes.ContainsKey("acquirer"));
        Assert.Equal(TextPolicy.None, assistance.Text);
    }

    /// <summary>kev-classes: the closed logical space keeps its 19 classes.</summary>
    [Fact]
    public void The_closed_product_class_space_keeps_its_values()
    {
        LogicalSpaceDefinition classes = Read("feeders/packages/kev-classes.yaml").LogicalSpaces[0];
        Assert.Equal("cyber.product_class", classes.Space);
        Assert.True(classes.Closed);
        Assert.Equal(19, classes.Values.Count);
        Assert.Contains("MFT", classes.Values);
    }

    /// <summary>core: the closed vocabularies and the shared namespaces are read.</summary>
    [Fact]
    public void The_core_package_reads_its_vocabularies_and_shared_namespaces()
    {
        SchemaPackage core = Read("schemas/seed/core.yaml");
        Assert.Equal(8, core.Vocabularies["precision"].Count);
        Assert.Contains("NO", core.Vocabularies["country"]);
        SharedNamespace cve = core.SharedEntityNamespaces[0];
        Assert.Equal("cve", cve.Namespace);
        Assert.Equal("Vulnerability", cve.EntityType);
        Assert.Equal("^CVE-[0-9]{4}-[0-9]{4,7}$", cve.KeyPattern);
    }

    /// <summary>A field the package format does not have is refused, never silently dropped.</summary>
    [Fact]
    public void An_unknown_field_is_refused()
    {
        string package = "package: demo\ntitle: Demo package\nversioning: immutable\n";
        Assert.Throws<YamlException>(() => PackageReader.Read(package));
    }

    /// <summary>Reads a package file given relative to the repository root.</summary>
    private static SchemaPackage Read(string file)
    {
        return PackageReader.Read(RepositoryFiles.ReadText(file));
    }

    /// <summary>The measure with this name in the package.</summary>
    private static MeasureDefinition FindMeasure(SchemaPackage package, string name)
    {
        foreach (MeasureDefinition measure in package.Measures)
        {
            if (measure.Name == name)
            {
                return measure;
            }
        }
        throw new KeyNotFoundException($"measure {name}");
    }

    /// <summary>The event type with this code in the package.</summary>
    private static EventTypeDefinition FindEventType(SchemaPackage package, string code)
    {
        foreach (EventTypeDefinition eventType in package.EventTypes)
        {
            if (eventType.Code == code)
            {
                return eventType;
            }
        }
        throw new KeyNotFoundException($"event type {code}");
    }
}
