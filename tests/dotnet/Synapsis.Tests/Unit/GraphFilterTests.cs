using System;
using System.Collections.Generic;
using Synapsis.Api.Models.Requests;
using Synapsis.Api.Repositories;
using Xunit;

namespace Synapsis.Tests.Unit;

/// <summary>Checks the supported Neo4j graph filter predicates used by graph queries.</summary>
public sealed class GraphFilterTests
{
    /// <summary>Event filters use a parameter for the requested type and source.</summary>
    [Fact]
    public void Event_type_and_source_filters_are_parameterized()
    {
        AnalyzeFilters filters = new AnalyzeFilters
        {
            Types = new List<string> { "TEST.EVENT" },
            Sources = new List<string> { "src:test" }
        };
        Dictionary<string, object?> parameters = new Dictionary<string, object?>();

        string where = GraphFilters.ForEvent(filters, "ev", parameters);

        Assert.Equal("ev.type IN $types AND ev.source IN $sources", where);
        Assert.Equal(filters.Types, parameters["types"]);
        Assert.Equal(filters.Sources, parameters["sources"]);
    }

    /// <summary>From is inclusive and to is exclusive for event starts.</summary>
    [Fact]
    public void Event_time_window_keeps_inclusive_from_and_exclusive_to()
    {
        DateTime from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = from.AddDays(1);
        AnalyzeFilters filters = new AnalyzeFilters { From = from, To = to };
        Dictionary<string, object?> parameters = new Dictionary<string, object?>();

        string where = GraphFilters.ForEvent(filters, "ev", parameters);

        Assert.Equal("ev.time_start >= $from AND ev.time_start < $to", where);
        Assert.Equal(from, parameters["from"]);
        Assert.Equal(to, parameters["to"]);
    }

    /// <summary>A kind filter that excludes events keeps no event groups.</summary>
    [Fact]
    public void Non_event_kind_filter_keeps_no_events()
    {
        AnalyzeFilters filters = new AnalyzeFilters { Kinds = new List<string> { "entity" } };
        Dictionary<string, object?> parameters = new Dictionary<string, object?>();

        string where = GraphFilters.ForEvent(filters, "ev", parameters);

        Assert.Equal("false", where);
        Assert.Empty(parameters);
    }

    /// <summary>Relation and participation predicates filter by as_of only.</summary>
    [Fact]
    public void Link_filter_uses_the_as_of_parameter()
    {
        DateTime asOf = new DateTime(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc);
        AnalyzeFilters filters = new AnalyzeFilters { AsOf = asOf };
        Dictionary<string, object?> parameters = new Dictionary<string, object?>();

        string where = GraphFilters.ForLink(filters, "link", parameters);

        Assert.Equal("link.known_at <= $as_of", where);
        Assert.Equal(asOf, parameters["as_of"]);
    }

    /// <summary>Keyword filtering is not applied by the current Neo4j graph filter helper.</summary>
    [Fact]
    public void Keyword_filter_is_not_applied_to_graph_events()
    {
        AnalyzeFilters filters = new AnalyzeFilters { Keywords = "not indexed in Neo4j" };
        Dictionary<string, object?> parameters = new Dictionary<string, object?>();

        string where = GraphFilters.ForEvent(filters, "ev", parameters);

        Assert.Equal("true", where);
        Assert.Empty(parameters);
    }
}
