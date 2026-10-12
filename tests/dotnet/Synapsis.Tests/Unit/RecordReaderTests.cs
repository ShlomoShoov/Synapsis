using System.Text.Json;
using Synapsis.Data.Models.Records;
using Synapsis.Data.Services;
using Xunit;

namespace Synapsis.Tests.Unit;

/// <summary>Checks conversion of stored JSON into the existing SIL record models.</summary>
public sealed class RecordReaderTests
{
    /// <summary>Checks that an entity JSON object becomes an EntityRecord.</summary>
    [Fact]
    public void An_entity_json_object_becomes_an_entity_record()
    {
        using JsonDocument document = JsonDocument.Parse("{\"kind\":\"entity\",\"id\":\"ent:1\",\"type\":\"PERSON\",\"source\":\"src:test\",\"name\":\"Ada\"}");

        (SilRecord? record, string? error) = RecordReader.Read(document.RootElement);

        EntityRecord entity = Assert.IsType<EntityRecord>(record);
        Assert.Null(error);
        Assert.Equal("ent:1", entity.Id);
        Assert.Equal("Ada", entity.Name);
    }

    /// <summary>Checks that an unsupported kind returns a plain conversion error.</summary>
    [Fact]
    public void An_unknown_record_kind_returns_a_conversion_error()
    {
        using JsonDocument document = JsonDocument.Parse("{\"kind\":\"unknown\"}");

        (SilRecord? record, string? error) = RecordReader.Read(document.RootElement);

        Assert.Null(record);
        Assert.Contains("kind must be entity", error);
    }
}
