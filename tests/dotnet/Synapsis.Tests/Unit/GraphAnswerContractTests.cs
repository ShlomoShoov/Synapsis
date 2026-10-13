using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using Synapsis.Api.Models.Dto;
using Synapsis.Api.Services;
using Synapsis.Data.Models.Entities;
using Xunit;

namespace Synapsis.Tests.Unit;

/// <summary>Checks the established flat JSON contract of a graph answer.</summary>
public sealed class GraphAnswerContractTests
{
    /// <summary>A conclusion statement uses a known entity name and leaves an unknown subject as its id.</summary>
    [Fact]
    public void Conclusion_statement_uses_known_entity_name()
    {
        const string conclusionId = "con:test:1";
        const string entityId = "ent:test:apache";
        const string unknownId = "ent:test:unknown";
        DateTime knownAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        StoredRecord statement = new StoredRecord
        {
            Key = conclusionId + "|src:test|1", RecordId = conclusionId, Source = "src:test",
            Kind = "conclusion", Type = "TEST.CONCLUSION", State = 1, KnownAt = knownAt,
            Record = new BsonDocument
            {
                ["kind"] = "conclusion", ["id"] = conclusionId, ["type"] = "TEST.CONCLUSION",
                ["source"] = "src:test", ["known_at"] = new BsonDateTime(knownAt),
                ["about"] = new BsonArray { entityId, unknownId },
                ["statement"] = new BsonDocument { ["predicate"] = "in_logical_space", ["object"] = "cyber.product_class/LIBRARY" }
            }
        };
        Dictionary<string, string> names = new Dictionary<string, string> { [entityId] = "Apache" };

        RecordSummary summary = GraphSummaryBuilder.Build(new List<StoredRecord> { statement }, names);

        Assert.Equal("Apache, ent:test:unknown: in_logical_space cyber.product_class/LIBRARY", summary.Label);
        Assert.Equal("statement", summary.LabelFrom);
    }

    /// <summary>Connection items stay flat and nullable fields are omitted from the serialized response.</summary>
    [Fact]
    public void Graph_answer_uses_flat_snake_case_fields_and_omits_nulls()
    {
        GraphAnswer answer = new GraphAnswer
        {
            AsOf = new DateTime(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc),
            Center = new RecordSummary
            {
                Id = "ent:test:center",
                Kind = "entity",
                Type = "Product",
                Label = "Center",
                LabelFrom = "name"
            },
            Connections = new List<ConnectionGroup>
            {
                new ConnectionGroup
                {
                    Key = "relation:PRODUCT_OF:out",
                    Kind = "relation",
                    Type = "PRODUCT_OF",
                    Label = "PRODUCT_OF →",
                    Count = 1,
                    Items = new List<ConnectedRecord>
                    {
                        new ConnectedRecord
                        {
                            Id = "ent:test:neighbor",
                            Kind = "entity",
                            Type = "Vendor",
                            Label = "Neighbor",
                            LabelFrom = "name",
                            Direction = "out"
                        }
                    }
                }
            }
        };
        JsonSerializerOptions options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        string json = JsonSerializer.Serialize(answer, options);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement group = root.GetProperty("connections")[0];
        JsonElement item = group.GetProperty("items")[0];

        Assert.Equal("2026-10-13T09:00:00Z", root.GetProperty("as_of").GetString());
        Assert.Equal("relation:PRODUCT_OF:out", group.GetProperty("key").GetString());
        Assert.Equal("out", item.GetProperty("direction").GetString());
        Assert.False(root.TryGetProperty("groups", out _));
        Assert.False(root.TryGetProperty("items", out _));
        Assert.False(root.TryGetProperty("reason", out _));
        Assert.False(group.TryGetProperty("next", out _));
        Assert.False(item.TryGetProperty("role", out _));
    }
}
