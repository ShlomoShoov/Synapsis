using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Synapsis.IntegrationTests.Support;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Domain.Records;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Versioning.Models.Domain;
using Synapsis.Storage.Database.Services.Dal;
using Synapsis.Storage.Records.Abstractions;
using Synapsis.Storage.Records.Models.Domain;
using Synapsis.Storage.Records.Models.Requests;
using Synapsis.Storage.Records.Services.Repositories;
using Xunit;

namespace Synapsis.IntegrationTests.Storage.Records;

/// <summary>The record repository against a real MongoDB: adding versions and every question asked of them.</summary>
public sealed class RecordRepositoryTests : IDisposable
{
    private const string QuakeId = "evt:usgs:q1";
    private const string Usgs = "src:usgs:earthquakes";
    private const string Emsc = "src:emsc:earthquakes";

    private readonly TestDatabase _database = new TestDatabase();
    private readonly IRecordRepository _repository;

    /// <summary>A repository over a fresh database.</summary>
    public RecordRepositoryTests()
    {
        _repository = new RecordRepository(new MongoContext(_database.Options));
    }

    /// <summary>Deletes the database.</summary>
    public void Dispose()
    {
        _database.Dispose();
    }

    /// <summary>Added versions come back as the stored versions of their statement, and only of theirs.</summary>
    [Fact]
    public async Task Added_versions_are_the_stored_versions_of_their_statement()
    {
        await AddAsync(Version(QuakeId, Usgs, 1, Day(2020)), Version(QuakeId, Usgs, 2, Day(2022)), Version(QuakeId, Emsc, 1, Day(2021)));
        StatementKey usgs = new StatementKey { Id = QuakeId, Source = Usgs };
        StatementKey unknown = new StatementKey { Id = "evt:usgs:none", Source = Usgs };
        IReadOnlyDictionary<StatementKey, IReadOnlyList<StoredVersion>> stored = await _repository.StoredVersionsAsync(new[] { usgs, unknown });
        Assert.Equal(2, stored[usgs].Count);
        Assert.False(stored.ContainsKey(unknown));
        Assert.Contains(stored[usgs], version => version.Version == 2 && version.KnownAt == Day(2022) && version.Fingerprint == "fp-2");
    }

    /// <summary>A version another writer stored first answers E-UNAVAILABLE ("send again").</summary>
    [Fact]
    public async Task A_version_stored_twice_is_unavailable()
    {
        await AddAsync(Version(QuakeId, Usgs, 1, Day(2020)));
        Result again = await _repository.AddAsync(new[] { Version(QuakeId, Usgs, 1, Day(2020)) });
        Assert.Equal(ErrorCodes.Unavailable, again.Problem!.Code);
    }

    /// <summary>As of a date: each source's latest version known by then; nothing before the first.</summary>
    [Fact]
    public async Task As_of_a_date_gives_each_sources_latest_version_known_then()
    {
        await AddAsync(Version(QuakeId, Usgs, 1, Day(2020)), Version(QuakeId, Usgs, 2, Day(2022)), Version(QuakeId, Emsc, 1, Day(2021)));
        IReadOnlyList<StoredStatement> in2021 = await _repository.AsOfAsync(new StatementsAsOfRequest(QuakeId, Day(2021), null));
        Assert.Equal(2, in2021.Count);
        Assert.Contains(in2021, statement => SourceOf(statement) == Usgs && statement.Version == 1);
        IReadOnlyList<StoredStatement> latest = await _repository.AsOfAsync(new StatementsAsOfRequest(QuakeId, null, Usgs));
        Assert.Equal(2, Assert.Single(latest).Version);
        Assert.Empty(await _repository.AsOfAsync(new StatementsAsOfRequest(QuakeId, Day(2019), null)));
    }

    /// <summary>A record comes back with the values it was stored with.</summary>
    [Fact]
    public async Task A_record_comes_back_as_stored()
    {
        await AddAsync(Version(QuakeId, Usgs, 1, Day(2020)));
        StoredStatement statement = Assert.Single(await _repository.AsOfAsync(new StatementsAsOfRequest(QuakeId, null, null)));
        JsonElement measure = statement.Record.GetProperty("measures")[0];
        Assert.Equal(7.5, measure.GetProperty("value").GetDouble());
        Assert.Equal(95, statement.Record.GetProperty("reliability").GetInt32());
        Assert.Equal("batch-1", statement.BatchId);
    }

    /// <summary>Existence covers every kind; entity types come from stored entities only.</summary>
    [Fact]
    public async Task Existence_and_entity_types_are_answered()
    {
        await AddAsync(Version(QuakeId, Usgs, 1, Day(2020)), Entity("ent:kev-vendor:progress", "CYB.VENDOR"));
        HashSet<string> existing = await _repository.ExistingAsync(new[] { QuakeId, "ent:kev-vendor:progress", "evt:usgs:none" });
        Assert.Equal(new HashSet<string> { QuakeId, "ent:kev-vendor:progress" }, existing);
        Dictionary<string, HashSet<string>> types = await _repository.EntityTypesAsync(new[] { "ent:kev-vendor:progress", QuakeId });
        Assert.Equal(new HashSet<string> { "CYB.VENDOR" }, types["ent:kev-vendor:progress"]);
        Assert.False(types.ContainsKey(QuakeId));
    }

    /// <summary>Adds versions that must be stored.</summary>
    private async Task AddAsync(params RecordVersion[] versions)
    {
        Result result = await _repository.AddAsync(versions);
        Assert.True(result.Succeeded, result.Problem?.Message);
    }

    /// <summary>A version of an earthquake statement.</summary>
    private static RecordVersion Version(string id, string source, int version, DateTime knownAt)
    {
        string json = $$"""{"kind": "event", "id": "{{id}}", "type": "GEO.EARTHQUAKE", "source": "{{source}}", "reliability": 95, "measures": [{"name": "magnitude", "value": 7.5, "unit": "magnitude"}]}""";
        return Make(id, source, RecordKind.Event, version, knownAt, json);
    }

    /// <summary>Version 1 of an entity of this type.</summary>
    private static RecordVersion Entity(string id, string type)
    {
        string json = $$"""{"kind": "entity", "id": "{{id}}", "type": "{{type}}", "source": "src:cisa:kev", "name": "Progress"}""";
        return Make(id, "src:cisa:kev", RecordKind.Entity, 1, Day(2023), json);
    }

    /// <summary>A version with the given content.</summary>
    private static RecordVersion Make(string id, string source, RecordKind kind, int version, DateTime knownAt, string json)
    {
        using JsonDocument record = JsonDocument.Parse(json);
        return new RecordVersion
        {
            Key = new StatementKey { Id = id, Source = source },
            Kind = kind,
            Version = version,
            KnownAt = knownAt,
            Fingerprint = "fp-" + version,
            Record = record.RootElement.Clone(),
            BatchId = "batch-" + version,
            Producer = "tests",
            ReceivedAt = knownAt,
        };
    }

    /// <summary>The source of a stored statement.</summary>
    private static string? SourceOf(StoredStatement statement)
    {
        return statement.Record.GetProperty("source").GetString();
    }

    /// <summary>1 January of a year, UTC.</summary>
    private static DateTime Day(int year)
    {
        return new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}
