using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Synapsis.Api.Models.Dto;
using Xunit;

namespace Synapsis.Tests.Unit;

/// <summary>Checks the established flat JSON contract of a graph answer.</summary>
public sealed class GraphAnswerContractTests
{
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
