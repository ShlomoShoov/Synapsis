using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Synapsis.Data.Services;
using Synapsis.MockApi.Models.Entities;

namespace Synapsis.MockApi.Repositories;

/// <summary>
/// The sample of real records (Sample/records.json), read once into memory when the mock starts. It answers what the
/// stores answer for the real API: the states known at a moment, like MongoDB's "records" collection.
/// </summary>
public sealed class SampleRepository
{
    /// <summary>Where the sample is, next to the program.</summary>
    public const string SampleFile = "Sample/records.json";

    private readonly List<SampleRecord> _sampleRecords;

    /// <summary>Reads the sample file and each record into its class (RecordReader, as the real services do).</summary>
    public SampleRepository()
    {
        // 1. Read the file (snake_case names, as MongoDB holds them).
        string text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, SampleFile));
        JsonSerializerOptions options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        _sampleRecords = JsonSerializer.Deserialize<List<SampleRecord>>(text, options) ?? new List<SampleRecord>();

        // 2. Read each record into its class once.
        foreach (SampleRecord sampleRecord in _sampleRecords)
        {
            sampleRecord.ReadRecord = RecordReader.Read(sampleRecord.Record).Record;
        }
    }

    /// <summary>For every record and source, the latest state known at the moment (known_at not after it).</summary>
    public List<SampleRecord> LatestAt(DateTime moment)
    {
        Dictionary<string, SampleRecord> latestByKey = new Dictionary<string, SampleRecord>();
        foreach (SampleRecord sampleRecord in _sampleRecords)
        {
            if (sampleRecord.KnownAt > moment || sampleRecord.ReadRecord == null)
            {
                continue;
            }
            string key = sampleRecord.Id + "|" + sampleRecord.Source;
            if (!latestByKey.ContainsKey(key) || latestByKey[key].State < sampleRecord.State)
            {
                latestByKey[key] = sampleRecord;
            }
        }
        return new List<SampleRecord>(latestByKey.Values);
    }

    /// <summary>
    /// Each source's latest state of one record known at the moment (no moment: the latest state), sorted by source,
    /// as GET /records/{id} answers. A source name keeps that source only. Empty when none.
    /// </summary>
    public List<SampleRecord> StatementsOf(string recordId, DateTime? moment, string? source)
    {
        List<SampleRecord> statements = new List<SampleRecord>();
        foreach (SampleRecord sampleRecord in LatestAt(moment ?? DateTime.MaxValue))
        {
            if (sampleRecord.Id == recordId && (source == null || sampleRecord.Source == source))
            {
                statements.Add(sampleRecord);
            }
        }
        statements.Sort(CompareBySource);
        return statements;
    }

    /// <summary>Orders statements by source name.</summary>
    private static int CompareBySource(SampleRecord first, SampleRecord second)
    {
        return string.CompareOrdinal(first.Source, second.Source);
    }
}
