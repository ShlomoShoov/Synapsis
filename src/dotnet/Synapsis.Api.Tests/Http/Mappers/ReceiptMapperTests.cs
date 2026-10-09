using System;
using System.Collections.Generic;
using System.Text.Json;
using Synapsis.Api.Http.Models.Configuration;
using Synapsis.Api.Http.Services.Mappers;
using Synapsis.Api.Ingestion.Models.Results;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Validation.Models.Results;
using Xunit;

namespace Synapsis.Api.Tests.Http.Mappers;

/// <summary>The receipt's body, with the names the feeders and the conformance suite read.</summary>
public class ReceiptMapperTests
{
    /// <summary>Every field has its snake_case name, and the status and time are text.</summary>
    [Fact]
    public void The_receipt_body_has_the_contract_names()
    {
        Receipt receipt = new Receipt
        {
            BatchId = "kev-0001",
            Status = ReceiptStatus.Partial,
            Producer = "feeder:kev",
            ReceivedAt = new DateTime(2026, 10, 9, 8, 0, 0, DateTimeKind.Utc),
            Counts = new ReceiptCounts { Received = 2, New = 1, Versioned = 0, Unchanged = 0, Duplicates = 0, Rejected = 1 },
            Rejected = new List<Rejection> { new Rejection(1, "evt:kev:x", new Problem("E-MEASURE", "measures[0].name", "unknown measure")) },
            DryRun = true,
        };
        JsonElement body = JsonSerializer.SerializeToElement(ReceiptMapper.ToDto(receipt), ApiJson.Options);
        Assert.Equal("kev-0001", body.GetProperty("batch_id").GetString());
        Assert.Equal("partial", body.GetProperty("status").GetString());
        Assert.Equal("2026-10-09T08:00:00Z", body.GetProperty("received_at").GetString());
        Assert.Equal(1, body.GetProperty("counts").GetProperty("new").GetInt32());
        Assert.True(body.GetProperty("dry_run").GetBoolean());
        JsonElement rejection = body.GetProperty("rejected")[0];
        Assert.Equal(1, rejection.GetProperty("index").GetInt32());
        Assert.Equal("E-MEASURE", rejection.GetProperty("code").GetString());
        Assert.Equal("measures[0].name", rejection.GetProperty("path").GetString());
    }
}
