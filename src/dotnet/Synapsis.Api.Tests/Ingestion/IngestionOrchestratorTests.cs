using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Synapsis.Api.Ingestion.Abstractions;
using Synapsis.Api.Ingestion.Models.Requests;
using Synapsis.Api.Ingestion.Models.Results;
using Synapsis.Api.Ingestion.Services.Domain;
using Synapsis.Api.Ingestion.Services.Orchestrators;
using Synapsis.Api.Tests.Support;
using Synapsis.Api.Vocabulary.Services.Providers;
using Synapsis.Sil;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Results;
using Xunit;

namespace Synapsis.Api.Tests.Ingestion;

/// <summary>The ingestion process with a record store in memory and the real record-language components.</summary>
public class IngestionOrchestratorTests
{
    /// <summary>Case 1 of 01-valid.json: a KEV entry of five records (three entities, a relation, the event).</summary>
    private const int KevEntry = 1;

    private readonly FakeRecordRepository _records = new FakeRecordRepository();
    private readonly IIngestionOrchestrator _ingestion;

    /// <summary>An ingestion over an empty store, with the vocabulary of every real package.</summary>
    public IngestionOrchestratorTests()
    {
        VocabularyProvider vocabulary = new VocabularyProvider();
        vocabulary.Replace(TestPackages.Vocabulary());
        _ingestion = new IngestionOrchestrator(SilComponents.CreateBundleReader(), vocabulary, SilComponents.CreateBatchValidator(_records), _records, SilComponents.CreateVersionPlanner(), new ReceiptBuilder(), new FixedClock());
    }

    /// <summary>A new batch is stored: every record is version 1, with its batch and receive time.</summary>
    [Fact]
    public async Task A_new_batch_is_stored()
    {
        Receipt receipt = await IngestAsync(TestBatches.ValidCase(KevEntry), false);
        Assert.Equal(ReceiptStatus.Accepted, receipt.Status);
        Assert.Equal(5, receipt.Counts.Received);
        Assert.Equal(5, receipt.Counts.New);
        Assert.Equal(5, _records.Versions.Count);
        Assert.All(_records.Versions, version => Assert.Equal(1, version.Version));
        Assert.Equal("test-batch-0001", _records.Versions[0].BatchId);
        Assert.Equal(FixedClock.Now.UtcDateTime, _records.Versions[0].ReceivedAt);
    }

    /// <summary>The same batch sent again stores nothing: every record is unchanged.</summary>
    [Fact]
    public async Task The_same_batch_again_is_unchanged()
    {
        await IngestAsync(TestBatches.ValidCase(KevEntry), false);
        Receipt again = await IngestAsync(TestBatches.ValidCase(KevEntry), false);
        Assert.Equal(ReceiptStatus.Unchanged, again.Status);
        Assert.Equal(5, again.Counts.Unchanged);
        Assert.Equal(5, _records.Versions.Count);
    }

    /// <summary>A record whose content changed becomes version 2; the others are unchanged.</summary>
    [Fact]
    public async Task A_changed_record_becomes_the_next_version()
    {
        await IngestAsync(TestBatches.ValidCase(KevEntry), false);
        JsonArray changed = TestBatches.ValidCase(KevEntry);
        changed[0]!["name"] = changed[0]!["name"]!.GetValue<string>() + " (renamed)";
        Receipt receipt = await IngestAsync(changed, false);
        Assert.Equal(1, receipt.Counts.Versioned);
        Assert.Equal(4, receipt.Counts.Unchanged);
        Assert.Equal(2, _records.Versions[5].Version);
    }

    /// <summary>A dry run answers what would be stored, and stores nothing.</summary>
    [Fact]
    public async Task A_dry_run_stores_nothing()
    {
        Receipt receipt = await IngestAsync(TestBatches.ValidCase(KevEntry), true);
        Assert.True(receipt.DryRun);
        Assert.Equal(5, receipt.Counts.New);
        Assert.Empty(_records.Versions);
    }

    /// <summary>A refused record makes the batch partial; the others are still stored.</summary>
    [Fact]
    public async Task A_refused_record_makes_the_batch_partial()
    {
        JsonArray records = TestBatches.ValidCase(KevEntry);
        JsonObject broken = TestBatches.ValidCase(0)[0]!.DeepClone().AsObject();
        broken["kind"] = "source";
        records.Add(broken);
        Receipt receipt = await IngestAsync(records, false);
        Assert.Equal(ReceiptStatus.Partial, receipt.Status);
        Assert.Equal(5, Assert.Single(receipt.Rejected).Index);
        Assert.Equal(5, _records.Versions.Count);
    }

    /// <summary>A batch whose every record is refused is rejected.</summary>
    [Fact]
    public async Task A_batch_of_refused_records_is_rejected()
    {
        JsonObject broken = TestBatches.ValidCase(0)[0]!.DeepClone().AsObject();
        broken["kind"] = "source";
        Receipt receipt = await IngestAsync(new JsonArray(broken), false);
        Assert.Equal(ReceiptStatus.Rejected, receipt.Status);
    }

    /// <summary>When another writer stored a version first, the answer is E-UNAVAILABLE: send again.</summary>
    [Fact]
    public async Task A_write_conflict_answers_unavailable()
    {
        _records.ConflictOnNextWrite = true;
        Result<Receipt> result = await _ingestion.IngestAsync(new IngestionRequest(TestBatches.Body(TestBatches.ValidCase(KevEntry)), false));
        Assert.Equal(ErrorCodes.Unavailable, result.Problem!.Code);
    }

    /// <summary>A body that is not a batch is refused whole.</summary>
    [Fact]
    public async Task A_body_that_is_not_a_batch_is_refused()
    {
        Result<Receipt> result = await _ingestion.IngestAsync(new IngestionRequest("{\"sil\": \"1.2\"}", false));
        Assert.Equal(ErrorCodes.Bundle, result.Problem!.Code);
    }

    /// <summary>Ingests records that make a valid batch.</summary>
    private async Task<Receipt> IngestAsync(JsonArray records, bool dryRun)
    {
        Result<Receipt> result = await _ingestion.IngestAsync(new IngestionRequest(TestBatches.Body(records), dryRun));
        Assert.True(result.Succeeded, $"{result.Problem?.Code} {result.Problem?.Message}");
        return result.Value!;
    }
}
