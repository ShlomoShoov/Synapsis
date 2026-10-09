using System;
using System.Collections.Generic;
using System.Text.Json;
using Synapsis.Sil;
using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Reading.Abstractions;
using Xunit;

namespace Synapsis.Tests.Reading;

/// <summary>
/// Checks that SIL JSON becomes the right typed classes, using the records of the conformance file 01-valid.json
/// (records every API must accept) and small hand-written records.
/// </summary>
public class RecordReaderTests
{
    /// <summary>The record reader, through its abstraction.</summary>
    private static readonly IRecordReader Reader = SilComponents.CreateRecordReader();

    /// <summary>The bundle reader, through its abstraction.</summary>
    private static readonly IBundleReader BundleReader = SilComponents.CreateBundleReader();

    /// <summary>Every valid conformance record reads into the class of its kind, with its id and source.</summary>
    [Fact]
    public void Every_valid_conformance_record_reads_into_the_class_of_its_kind()
    {
        List<JsonElement> records = ValidConformanceRecords();
        Assert.NotEmpty(records);
        foreach (JsonElement record in records)
        {
            SilRecord typed = Read(record);
            Assert.Equal(record.GetProperty("kind").GetString(), KindName(typed));
            Assert.Equal(record.GetProperty("id").GetString(), typed.Id);
            Assert.Equal(record.GetProperty("source").GetString(), typed.Source);
        }
    }

    /// <summary>A KEV catalog event keeps its time, logical place, participants, text and attributes.</summary>
    [Fact]
    public void A_kev_event_reads_with_all_its_parts()
    {
        EventRecord kev = Assert.IsType<EventRecord>(Read(Parse(KevEvent)));
        Assert.Equal(RecordKind.Event, kev.Kind);
        Assert.Equal("2023-06-02T00:00:00Z", kev.Time.Start);
        Assert.Equal("day", kev.Time.Precision);
        Assert.Equal("cyber.vendor_product", kev.Place.Logical!.Space);
        Assert.Equal(new List<string> { "Progress", "MOVEit Transfer" }, kev.Place.Logical.Path);
        Assert.Equal("affected_product", kev.Participants![1].Role);
        Assert.Equal("Progress MOVEit Transfer SQL Injection Vulnerability", kev.Text!.Title);
        Assert.Equal("CWE-89", kev.Attributes!["cwes"][0].GetString());
        Assert.Equal("published", kev.KnownAtBasis);
    }

    /// <summary>An earthquake keeps its point, its uncertainty and its measures.</summary>
    [Fact]
    public void An_earthquake_reads_with_its_point_and_measures()
    {
        EventRecord quake = Assert.IsType<EventRecord>(Read(Parse(Earthquake)));
        Assert.Equal(new double[] { 137.27, 37.5 }, quake.Place.Point);
        Assert.Equal(5.0, quake.Place.UncertaintyKm);
        Assert.Equal("magnitude", quake.Measures![0].Name);
        Assert.Equal(7.5, quake.Measures[0].Value);
        Assert.Equal("magnitude", quake.Measures[0].Unit);
    }

    /// <summary>A classification conclusion keeps what it is about, its statement, premises, method and confidence.</summary>
    [Fact]
    public void A_conclusion_reads_with_its_statement_and_method()
    {
        ConclusionRecord label = Assert.IsType<ConclusionRecord>(Read(Parse(Classification)));
        Assert.Equal("ent:kev-product:progress/moveit-transfer", label.About[0]);
        Assert.Equal("in_logical_space", label.Statement.Predicate);
        Assert.Equal("cyber.product_class/MFT", label.Statement.Object);
        Assert.Null(label.Statement.Value);
        Assert.Equal("evt:kev:CVE-2023-34362", label.Premises[0]);
        Assert.Equal("agent", label.Method.By);
        Assert.Equal(90.0, label.Confidence);
    }

    /// <summary>A bundle reads with its envelope fields, and its records stay raw JSON.</summary>
    [Fact]
    public void A_bundle_reads_and_keeps_its_records_raw()
    {
        string json = "{\"sil\": \"1.2\", \"batch_id\": \"kev-0001\", \"producer\": \"feeder:kev\", \"records\": ["
                      + KevEvent + ", " + Earthquake + "]}";
        IngestBundle bundle = BundleReader.Read(json).Value!;
        Assert.Equal("1.2", bundle.Sil);
        Assert.Equal("kev-0001", bundle.BatchId);
        Assert.Equal("feeder:kev", bundle.Producer);
        Assert.Equal(2, bundle.Records.Count);
        Assert.Equal(JsonValueKind.Object, bundle.Records[0].ValueKind);
    }

    /// <summary>A field the record language does not have is refused (E-SCHEMA), never silently dropped.</summary>
    [Fact]
    public void An_unknown_field_is_refused()
    {
        string withExtraField = Earthquake.Replace("\"kind\": \"event\",", "\"kind\": \"event\", \"severity\": 3,");
        Result<SilRecord> result = Reader.Read(Parse(withExtraField));
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.Schema, result.Problem!.Code);
    }

    /// <summary>A record whose kind is not one of the four is refused (E-KIND).</summary>
    [Fact]
    public void An_unknown_kind_is_refused()
    {
        string asSource = Earthquake.Replace("\"kind\": \"event\"", "\"kind\": \"source\"");
        Assert.Equal(ErrorCodes.Kind, Reader.Read(Parse(asSource)).Problem!.Code);
    }

    /// <summary>A body that is not a bundle is refused (E-BUNDLE).</summary>
    [Fact]
    public void A_body_that_is_not_a_bundle_is_refused()
    {
        Assert.Equal(ErrorCodes.Bundle, BundleReader.Read("{\"sil\": ").Problem!.Code);
    }

    /// <summary>Reads a record that must be readable.</summary>
    private static SilRecord Read(JsonElement record)
    {
        Result<SilRecord> result = Reader.Read(record);
        Assert.True(result.Succeeded, result.Problem?.Message);
        return result.Value!;
    }

    /// <summary>The records of tests/conformance/cases/01-valid.json, which every API must accept.</summary>
    private static List<JsonElement> ValidConformanceRecords()
    {
        List<JsonElement> records = new List<JsonElement>();
        JsonElement file = Parse(RepositoryFiles.ReadText("tests/conformance/cases/01-valid.json"));
        foreach (JsonElement testCase in file.GetProperty("cases").EnumerateArray())
        {
            foreach (JsonElement record in testCase.GetProperty("records").EnumerateArray())
            {
                records.Add(record);
            }
        }
        return records;
    }

    /// <summary>The kind of a typed record in its JSON spelling.</summary>
    private static string KindName(SilRecord record)
    {
        return record.Kind.ToString().ToLowerInvariant();
    }

    /// <summary>Parses JSON text into an element that stays valid after parsing.</summary>
    private static JsonElement Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>CVE-2023-34362 (MOVEit) as the KEV feeder sends it, shortened.</summary>
    private const string KevEvent = """
        {"kind": "event", "id": "evt:kev:CVE-2023-34362", "type": "CYB.VULN.KNOWN_EXPLOITED", "source": "src:cisa:kev",
         "reliability": 95, "reliability_basis": "A1", "known_at": "2023-06-03T00:00:00Z", "known_at_basis": "published",
         "time": {"start": "2023-06-02T00:00:00Z", "precision": "day"},
         "place": {"logical": {"space": "cyber.vendor_product", "path": ["Progress", "MOVEit Transfer"]}},
         "participants": [{"entity": "ent:cve:CVE-2023-34362", "role": "subject"},
                          {"entity": "ent:kev-product:progress/moveit-transfer", "role": "affected_product"},
                          {"entity": "ent:kev-vendor:progress", "role": "vendor"}],
         "text": {"title": "Progress MOVEit Transfer SQL Injection Vulnerability", "lang": "en"},
         "attributes": {"due_date": "2023-06-23", "known_ransomware_campaign_use": "Known", "cwes": ["CWE-89"]},
         "evidence": {"url": "https://www.cisa.gov/known-exploited-vulnerabilities-catalog", "ref": "CVE-2023-34362"}}
        """;

    /// <summary>An earthquake with a point, its uncertainty and two measures.</summary>
    private const string Earthquake = """
        {"kind": "event", "id": "evt:usgs:example-quake", "type": "GEO.EARTHQUAKE", "source": "src:usgs:comcat",
         "reliability": 95, "reliability_basis": "measured", "known_at": "2024-01-01T08:10:09Z",
         "known_at_basis": "proxy_publication_lag", "time": {"start": "2024-01-01T07:10:09Z", "precision": "second"},
         "place": {"point": [137.27, 37.5], "uncertainty_km": 5},
         "measures": [{"name": "magnitude", "value": 7.5, "unit": "magnitude"}, {"name": "depth_km", "value": 10, "unit": "km"}]}
        """;

    /// <summary>A product class label, as the kev-classes feeder sends it.</summary>
    private const string Classification = """
        {"kind": "conclusion", "id": "con:kev-classes:progress/moveit-transfer", "type": "classification",
         "source": "src:synapsis:kev-classes", "reliability": 90, "reliability_basis": "model",
         "known_at": "2026-10-02T00:00:00Z", "known_at_basis": "observed",
         "about": ["ent:kev-product:progress/moveit-transfer"],
         "statement": {"predicate": "in_logical_space", "object": "cyber.product_class/MFT"},
         "premises": ["evt:kev:CVE-2023-34362"],
         "method": {"by": "agent", "name": "teacher:claude-sonnet-5-5", "version": "r3"}, "confidence": 90}
        """;
}
