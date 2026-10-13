using System;
using System.Collections.Generic;
using System.Text.Json;
using H3;
using H3.Model;
using NetTopologySuite.Geometries;
using Synapsis.Data.Models.Entities;
using Synapsis.Data.Models.Records;
using Synapsis.Data.Services;
using Synapsis.Elasticsearch;
using Xunit;

namespace Synapsis.Tests.Unit;

/// <summary>Checks how a conclusion becomes a search document, so the map, entity, date and graph routes show it.</summary>
public sealed class SearchDocumentMapperTests
{
    /// <summary>A Rat Islands cell at resolution 3 and its resolution-2 parent, as the indexer wrote them for USGS events (§12).</summary>
    private const string RatIslandsR3 = "83225bfffffffff";
    private const string RatIslandsR2 = "82225ffffffffff";

    /// <summary>A cell in about places the conclusion at the cell's centre, with the cell and its coarser parents.</summary>
    [Fact]
    public void About_cell_gives_location_and_cells()
    {
        SearchDocument document = Map("""{"about": ["ent:h3:83225bfffffffff", "evt:usgs:x"]}""");

        Assert.NotNull(document.Location);
        Assert.Equal(RatIslandsR3, LatticeKeys.Cell(document.Location!, 0, 3));
        Assert.Equal(RatIslandsR2, document.H3R2);
        Assert.Equal(RatIslandsR3, document.H3R3);
        Assert.Null(document.H3R4);
        Assert.Null(document.H3R5);
        Assert.Equal(new List<string> { "ent:h3:83225bfffffffff", "evt:usgs:x" }, document.Entities);
    }

    /// <summary>The SIL subject key names a cell too; a value that is not a valid cell is skipped for the next one.</summary>
    [Fact]
    public void Subject_cell_is_accepted_and_an_invalid_cell_is_skipped()
    {
        SearchDocument document = Map("""{"about": ["ent:h3:nothex", "subject:h3:83225bfffffffff"]}""");

        Assert.Equal(RatIslandsR3, document.H3R3);
        Assert.Equal(2, document.Entities.Count);
    }

    /// <summary>A conclusion about record ids only has entities and no place.</summary>
    [Fact]
    public void About_ids_without_a_cell_give_entities_only()
    {
        SearchDocument document = Map("""{"about": ["ent:fdic:cert-1", "ent:fdic:cert-1"]}""");

        Assert.Null(document.Location);
        Assert.Null(document.H3R2);
        Assert.Equal(new List<string> { "ent:fdic:cert-1" }, document.Entities);
    }

    /// <summary>The period's start and precision give time_start, time_precision and the week.</summary>
    [Fact]
    public void Period_start_gives_time_and_week()
    {
        SearchDocument document = Map("""{"attributes": {"period_start": "1965-02-03", "period_precision": "week"}}""");

        Assert.Equal(new DateTime(1965, 2, 3, 0, 0, 0, DateTimeKind.Utc), document.TimeStart);
        Assert.Equal(DateTimeKind.Utc, document.TimeStart!.Value.Kind);
        Assert.Equal("week", document.TimePrecision);
        Assert.Equal(new DateTime(1965, 2, 1, 0, 0, 0, DateTimeKind.Utc), document.Week);
    }

    /// <summary>Without a period, known_at is the start, at a day's precision.</summary>
    [Fact]
    public void Without_a_period_known_at_is_the_start()
    {
        SearchDocument document = Map("""{"attributes": {"period_start": "not a time"}}""");

        Assert.Equal(new DateTime(1965, 2, 8, 0, 0, 0, DateTimeKind.Utc), document.TimeStart);
        Assert.Equal("day", document.TimePrecision);
        Assert.Equal(new DateTime(1965, 2, 8, 0, 0, 0, DateTimeKind.Utc), document.Week);
    }

    /// <summary>Each numeric field of statement.value is a measure, with the confidence; other fields are left out.</summary>
    [Fact]
    public void Statement_numbers_and_confidence_become_measures()
    {
        SearchDocument document = Map("""{"statement": {"predicate": "anomaly", "value": {"observed": 14, "expected": 0.4, "label": "high", "flags": [1]}}, "confidence": 99}""");

        Assert.Equal(new List<double> { 14 }, document.Measures["observed"]);
        Assert.Equal(new List<double> { 0.4 }, document.Measures["expected"]);
        Assert.Equal(new List<double> { 99 }, document.Measures["confidence"]);
        Assert.Equal(3, document.Measures.Count);
    }

    /// <summary>A bare number in statement.value is the measure "value".</summary>
    [Fact]
    public void Bare_statement_value_is_the_measure_value()
    {
        SearchDocument document = Map("""{"statement": {"predicate": "anomaly", "value": 12.3}}""");

        Assert.Equal(new List<double> { 12.3 }, document.Measures["value"]);
        Assert.False(document.Measures.ContainsKey("confidence"));
    }

    /// <summary>attributes.logical gives the logical prefixes.</summary>
    [Fact]
    public void Logical_attribute_gives_prefixes()
    {
        SearchDocument document = Map("""{"attributes": {"logical": {"space": "finance.bank", "path": ["US", "1234"]}}}""");

        Assert.Equal(new List<string> { "finance.bank", "finance.bank/US", "finance.bank/US/1234" }, document.LogicalPrefixes);
    }

    /// <summary>A text that is not an H3 cell gives no centre, and a finer cell gives its parents at 2 to 5.</summary>
    [Fact]
    public void Cell_centre_and_parents()
    {
        Assert.Null(LatticeKeys.CellCentreAndParents("nothex"));
        Assert.Null(LatticeKeys.CellCentreAndParents("ffffffffffffffffffff"));
        string finer = H3Index.FromLatLng(LatLng.FromCoordinate(new Coordinate(-179.67, 51.68)), 7).ToString();
        (List<double> Centre, Dictionary<int, string> Cells)? cell = LatticeKeys.CellCentreAndParents(finer);
        Assert.NotNull(cell);
        Assert.Equal(new[] { 2, 3, 4, 5 }, cell!.Value.Cells.Keys);
        Assert.Equal(RatIslandsR3, cell.Value.Cells[3]);
    }

    /// <summary>Reads the conclusion as the indexer does (the record reader) and maps it with a fixed envelope.</summary>
    private static SearchDocument Map(string fields)
    {
        Dictionary<string, JsonElement> record = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
            {"kind": "conclusion", "id": "con:engine-test:x", "type": "spark", "source": "src:synapsis:engine",
             "known_at": "1965-02-08T00:00:00Z", "about": ["ent:h3:83225bfffffffff"], "statement": {"predicate": "anomaly"},
             "premises": ["evt:usgs:x"]}
            """)!;
        foreach (KeyValuePair<string, JsonElement> field in JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(fields)!)
        {
            record[field.Key] = field.Value;
        }
        (SilRecord? read, string? error) = RecordReader.Read(JsonSerializer.SerializeToElement(record));
        Assert.Null(error);
        StoredRecord stored = new StoredRecord
        {
            RecordId = "con:engine-test:x", Source = "src:synapsis:engine", Kind = "conclusion", Type = "spark", State = 1,
            KnownAt = new DateTime(1965, 2, 8, 0, 0, 0, DateTimeKind.Utc)
        };
        return SearchDocumentMapper.Map(stored, read!);
    }
}
