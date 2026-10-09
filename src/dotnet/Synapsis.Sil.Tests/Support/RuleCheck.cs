using System;
using System.Text.Json.Nodes;
using Synapsis.Sil.Models.Domain.Records;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Validation.Abstractions;
using Synapsis.Sil.Validation.Models.Requests;
using Xunit;

namespace Synapsis.Sil.Tests.Support;

/// <summary>Runs one record through the record validator (rules 1 to 12) with the real registry.</summary>
public static class RuleCheck
{
    /// <summary>The record validator, built once through the composition.</summary>
    private static readonly IRecordValidator Validator = SilComponents.CreateRecordValidator();

    /// <summary>The problem the record gets, or null when it passes.</summary>
    public static Problem? ErrorOf(JsonObject record)
    {
        RecordValidationRequest request = new RecordValidationRequest(TestRecords.ToElement(record), TestRegistry.All, DateTime.UtcNow);
        Result<SilRecord> result = Validator.Validate(request);
        return result.Problem;
    }

    /// <summary>Asserts the record is refused with this code at this path.</summary>
    public static void Refused(JsonObject record, string code, string path)
    {
        Problem? problem = ErrorOf(record);
        Assert.NotNull(problem);
        Assert.Equal(code, problem.Code);
        Assert.Equal(path, problem.Path);
    }
}
