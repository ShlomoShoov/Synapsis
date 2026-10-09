using System;
using System.Text.Json.Nodes;
using Synapsis.Sil.Records;
using Synapsis.Sil.Validation;
using Xunit;

namespace Synapsis.Tests.Support;

/// <summary>Runs one record through the record validator (rules 1 to 12) with the real registry.</summary>
public static class RuleCheck
{
    /// <summary>The error the record gets, or null when it passes.</summary>
    public static RecordError? ErrorOf(JsonObject record)
    {
        return RecordValidator.Check(TestRecords.ToElement(record), TestRegistry.All, DateTime.UtcNow, out SilRecord? _);
    }

    /// <summary>Asserts the record is refused with this code at this path.</summary>
    public static void Refused(JsonObject record, string code, string path)
    {
        RecordError? error = ErrorOf(record);
        Assert.NotNull(error);
        Assert.Equal(code, error.Code);
        Assert.Equal(path, error.Path);
    }
}
