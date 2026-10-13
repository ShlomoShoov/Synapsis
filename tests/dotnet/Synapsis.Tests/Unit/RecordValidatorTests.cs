using System.Text.Json;
using Synapsis.Api.Models.Results;
using Synapsis.Api.Services;
using Xunit;

namespace Synapsis.Tests.Unit;

/// <summary>Step 1, validation: reading each record as its kind, its required fields, and copies inside one batch.</summary>
public sealed class RecordValidatorTests
{
    [Theory]
    [InlineData("entity")]
    [InlineData("event")]
    [InlineData("relation")]
    [InlineData("conclusion")]
    public void A_valid_record_of_each_kind_is_accepted(string kind)
    {
        ValidationResult result = RecordValidator.Validate(TestRecords.List(TestRecords.OfKind(kind)));

        Assert.Single(result.Valid);
        Assert.Equal(kind, result.Valid[0].Record.Kind);
        Assert.Empty(result.Rejected);
    }

    [Fact]
    public void An_unknown_kind_is_refused()
    {
        JsonElement record = TestRecords.With(TestRecords.OfKind("entity"), "kind", "\"source\"");

        AssertRefused(record, RecordValidator.CannotRead);
    }

    [Fact]
    public void A_record_that_is_not_an_object_is_refused()
    {
        AssertRefused(TestRecords.Parse("42"), RecordValidator.CannotRead);
    }

    [Fact]
    public void A_field_of_the_wrong_type_is_refused()
    {
        JsonElement record = TestRecords.With(TestRecords.OfKind("event"), "time", "12");

        AssertRefused(record, RecordValidator.CannotRead);
    }

    [Theory]
    [InlineData("1e400")]
    [InlineData("-1e400")]
    public void A_number_that_is_not_finite_is_refused(string number)
    {
        JsonElement record = TestRecords.With(TestRecords.OfKind("event"), "measures",
            "[{\"name\": \"magnitude\", \"value\": " + number + "}]");

        AssertRefused(record, RecordValidator.CannotRead);
    }

    [Fact]
    public void A_known_at_that_is_not_a_date_is_refused()
    {
        JsonElement record = TestRecords.With(TestRecords.OfKind("entity"), "known_at", "\"yesterday\"");

        AssertRefused(record, RecordValidator.CannotRead);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("type")]
    [InlineData("source")]
    [InlineData("known_at")]
    public void A_missing_shared_field_is_refused(string field)
    {
        JsonElement record = TestRecords.Without(TestRecords.OfKind("entity"), field);

        ValidationResult result = AssertRefused(record, RecordValidator.MissingField);
        Assert.Contains(field, result.Rejected[0].Reason);
    }

    [Fact]
    public void An_empty_id_is_refused()
    {
        JsonElement record = TestRecords.With(TestRecords.OfKind("entity"), "id", "\"\"");

        AssertRefused(record, RecordValidator.MissingField);
    }

    [Fact]
    public void An_entity_without_a_name_is_refused()
    {
        AssertRefused(TestRecords.Without(TestRecords.OfKind("entity"), "name"), RecordValidator.MissingField);
    }

    [Fact]
    public void An_event_without_a_time_start_is_refused()
    {
        JsonElement record = TestRecords.With(TestRecords.OfKind("event"), "time", "{\"precision\":\"day\"}");

        AssertRefused(record, RecordValidator.MissingField);
    }

    [Theory]
    [InlineData("from")]
    [InlineData("to")]
    public void A_relation_without_one_of_its_ends_is_refused(string field)
    {
        AssertRefused(TestRecords.Without(TestRecords.OfKind("relation"), field), RecordValidator.MissingField);
    }

    [Theory]
    [InlineData("about")]
    [InlineData("statement")]
    public void A_conclusion_without_about_or_statement_is_refused(string field)
    {
        AssertRefused(TestRecords.Without(TestRecords.OfKind("conclusion"), field), RecordValidator.MissingField);
    }

    [Fact]
    public void An_exact_copy_in_one_batch_is_counted_as_a_duplicate()
    {
        JsonElement record = TestRecords.OfKind("entity");

        ValidationResult result = RecordValidator.Validate(TestRecords.List(record, record));

        Assert.Single(result.Valid);
        Assert.Single(result.Duplicates);
        Assert.Empty(result.Rejected);
    }

    [Fact]
    public void The_same_id_with_other_content_in_one_batch_is_refused()
    {
        JsonElement first = TestRecords.OfKind("entity");
        JsonElement second = TestRecords.With(first, "name", "\"Another name\"");

        ValidationResult result = RecordValidator.Validate(TestRecords.List(first, second));

        Assert.Single(result.Valid);
        Assert.Single(result.Rejected);
        Assert.Equal(RecordValidator.DuplicateId, result.Rejected[0].Code);
        Assert.Equal(1, result.Rejected[0].Index);
    }

    [Fact]
    public void The_same_id_from_two_sources_is_two_records()
    {
        JsonElement first = TestRecords.Entity("ent:test:one", "src:test:data");
        JsonElement second = TestRecords.Entity("ent:test:one", "src:test:other");

        ValidationResult result = RecordValidator.Validate(TestRecords.List(first, second));

        Assert.Equal(2, result.Valid.Count);
    }

    [Fact]
    public void A_refused_record_keeps_its_index_id_and_source()
    {
        JsonElement good = TestRecords.Entity("ent:test:one", "src:test:data");
        JsonElement bad = TestRecords.Without(TestRecords.Entity("ent:test:two", "src:test:data"), "name");

        ValidationResult result = RecordValidator.Validate(TestRecords.List(good, bad));

        Assert.Equal(1, result.Rejected[0].Index);
        Assert.Equal("ent:test:two", result.Rejected[0].Id);
        Assert.Equal("src:test:data", result.Rejected[0].Source);
    }

    [Fact]
    public void A_record_without_a_readable_source_is_counted_under_unknown()
    {
        ValidationResult result = AssertRefused(TestRecords.Parse("[1, 2]"), RecordValidator.CannotRead);

        Assert.Equal("unknown", result.Rejected[0].Source);
        Assert.Null(result.Rejected[0].Id);
    }

    [Fact]
    public void The_valid_record_keeps_its_json()
    {
        JsonElement record = TestRecords.OfKind("event");

        ValidationResult result = RecordValidator.Validate(TestRecords.List(TestRecords.Parse("1"), record));

        Assert.Equal(record.GetRawText(), result.Valid[0].Json.GetRawText());
    }

    /// <summary>Validates a batch of one record and checks it was refused with the given code.</summary>
    private static ValidationResult AssertRefused(JsonElement record, string code)
    {
        ValidationResult result = RecordValidator.Validate(TestRecords.List(record));
        Assert.Empty(result.Valid);
        Assert.Single(result.Rejected);
        Assert.Equal(code, result.Rejected[0].Code);
        Assert.False(string.IsNullOrWhiteSpace(result.Rejected[0].Reason));
        return result;
    }
}
