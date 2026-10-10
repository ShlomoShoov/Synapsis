using System.Collections.Generic;
using System.Text.Json.Nodes;
using Synapsis.Api.Models.Dtos;
using Synapsis.Sil.Services.Time;
using Synapsis.Storage.Models;
namespace Synapsis.Api.Services.Http.Mappers;

/// <summary>Turns stored statements into the body of GET /records/{id}.</summary>
public static class StatementsMapper
{
    /// <summary>The statements of one id, as known at the date asked.</summary>
    public static StatementsDto ToDto(string id, string? asOf, IReadOnlyList<StoredStatement> statements)
    {
        List<JsonObject> bodies = new List<JsonObject>();
        foreach (StoredStatement statement in statements)
        {
            bodies.Add(ToJson(statement));
        }
        return new StatementsDto { Id = id, AsOf = asOf, Statements = bodies };
    }

    /// <summary>The record's fields, with its version and where it came from added beside them.</summary>
    private static JsonObject ToJson(StoredStatement statement)
    {
        JsonObject record = JsonNode.Parse(statement.Record.GetRawText())!.AsObject();
        record["version"] = statement.Version;
        record["_ingest"] = new JsonObject
        {
            ["batch_id"] = statement.BatchId,
            ["producer"] = statement.Producer,
            ["received_at"] = Instant.Format(statement.ReceivedAt),
        };
        return record;
    }
}
