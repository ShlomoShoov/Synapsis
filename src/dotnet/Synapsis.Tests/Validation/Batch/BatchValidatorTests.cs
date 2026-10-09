using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Synapsis.Sil.Validation;
using Synapsis.Sil.Validation.Batch;
using Synapsis.Tests.Support;
using Xunit;

namespace Synapsis.Tests.Validation.Batch;

/// <summary>Rule 13 and the batch as a whole: duplicates and references.</summary>
public class BatchValidatorTests
{
    /// <summary>A whole KEV entry with its classification is accepted (references resolved inside the batch).</summary>
    [Fact]
    public async Task A_complete_batch_is_accepted()
    {
        BatchResult result = await Validate(TestRecords.KevEntryWithClassification(), new FakeResolver());
        Assert.Empty(result.Rejected);
        Assert.Equal(6, result.Accepted.Count);
    }

    /// <summary>An identical copy is counted, not refused.</summary>
    [Fact]
    public async Task An_identical_copy_is_counted()
    {
        List<JsonObject> records = new List<JsonObject> { TestRecords.Earthquake(), TestRecords.Earthquake() };
        BatchResult result = await Validate(records, new FakeResolver());
        Assert.Single(result.Accepted);
        Assert.Empty(result.Rejected);
        Assert.Equal(1, result.Duplicates);
    }

    /// <summary>The same id with other content in one batch: the later copy is E-ID-CONFLICT.</summary>
    [Fact]
    public async Task The_same_id_with_other_content_is_refused()
    {
        JsonObject changed = TestRecords.Earthquake();
        changed["measures"]![0]!["value"] = 6.1;
        BatchResult result = await Validate(new List<JsonObject> { TestRecords.Earthquake(), changed }, new FakeResolver());
        Rejection rejection = Assert.Single(result.Rejected);
        Assert.Equal(1, rejection.Index);
        Assert.Equal(ErrorCodes.IdConflict, rejection.Code);
    }

    /// <summary>An event naming an entity that exists nowhere is E-REF-MISSING at that participant.</summary>
    [Fact]
    public async Task A_missing_entity_is_refused()
    {
        BatchResult result = await Validate(new List<JsonObject> { TestRecords.KevEvent() }, new FakeResolver());
        Rejection rejection = Assert.Single(result.Rejected);
        Assert.Equal(ErrorCodes.RefMissing, rejection.Code);
        Assert.Equal("participants[0].entity", rejection.Path);
    }

    /// <summary>Entities already stored satisfy the references (asked through the resolver).</summary>
    [Fact]
    public async Task Stored_entities_satisfy_references()
    {
        FakeResolver store = new FakeResolver();
        List<JsonObject> entry = TestRecords.KevEntry();
        for (int index = 0; index < 3; index++)
        {
            store.StoredEntities[entry[index]["id"]!.GetValue<string>()] = new HashSet<string> { entry[index]["type"]!.GetValue<string>() };
        }
        BatchResult result = await Validate(new List<JsonObject> { TestRecords.KevEvent() }, store);
        Assert.Empty(result.Rejected);
    }

    /// <summary>An entity of a type the role does not take is E-ROLE.</summary>
    [Fact]
    public async Task An_entity_of_the_wrong_type_for_its_role_is_refused()
    {
        List<JsonObject> entry = TestRecords.KevEntry();
        entry[4]["participants"]![0]!["entity"] = entry[1]["id"]!.GetValue<string>();
        BatchResult result = await Validate(entry, new FakeResolver());
        Rejection rejection = Assert.Single(result.Rejected);
        Assert.Equal(ErrorCodes.Role, rejection.Code);
        Assert.Equal("participants[0].entity", rejection.Path);
    }

    /// <summary>A relation end of a type the relation does not allow is E-RELATION-DOMAIN.</summary>
    [Fact]
    public async Task A_relation_end_of_the_wrong_type_is_refused()
    {
        List<JsonObject> entry = TestRecords.KevEntry();
        entry[3]["from"] = entry[0]["id"]!.GetValue<string>();
        BatchResult result = await Validate(entry, new FakeResolver());
        Assert.Equal(ErrorCodes.RelationDomain, result.Rejected[0].Code);
        Assert.Equal("from", result.Rejected[0].Path);
    }

    /// <summary>A conclusion whose premise exists nowhere is E-PREMISE-MISSING.</summary>
    [Fact]
    public async Task A_missing_premise_is_refused()
    {
        List<JsonObject> records = TestRecords.KevEntryWithClassification();
        records[5]["premises"] = new JsonArray("evt:kev:CVE-2099-9999");
        BatchResult result = await Validate(records, new FakeResolver());
        Rejection rejection = Assert.Single(result.Rejected);
        Assert.Equal(ErrorCodes.PremiseMissing, rejection.Code);
        Assert.Equal("premises[0]", rejection.Path);
    }

    /// <summary>Runs the batch validator over the records.</summary>
    private static Task<BatchResult> Validate(List<JsonObject> records, FakeResolver resolver)
    {
        List<JsonElement> raw = new List<JsonElement>();
        foreach (JsonObject record in records)
        {
            raw.Add(TestRecords.ToElement(record));
        }
        return BatchValidator.ValidateAsync(raw, TestRegistry.All, resolver, DateTime.UtcNow);
    }
}
