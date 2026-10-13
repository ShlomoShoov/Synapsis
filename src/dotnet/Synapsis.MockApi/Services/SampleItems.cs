using System.Collections.Generic;
using System.Text.RegularExpressions;
using Synapsis.Data.Models.Records;
using Synapsis.MockApi.Models.Entities;
using Synapsis.MockApi.Models.Results;

namespace Synapsis.MockApi.Services;

/// <summary>
/// Turns the statements known at an as_of into one item per record id (entities, events, conclusions), as the real
/// API's search index would hold them. Relations are links, not items: the graph reads them from the statements.
/// </summary>
public static class SampleItems
{
    /// <summary>One item per record id, by id.</summary>
    public static Dictionary<string, SampleItem> Build(List<SampleRecord> latest)
    {
        // 1. Each record's statements, by id (relations left out).
        Dictionary<string, List<SampleRecord>> statementsById = new Dictionary<string, List<SampleRecord>>();
        foreach (SampleRecord statement in latest)
        {
            if (statement.Kind == "relation")
            {
                continue;
            }
            if (!statementsById.ContainsKey(statement.Id))
            {
                statementsById.Add(statement.Id, new List<SampleRecord>());
            }
            statementsById[statement.Id].Add(statement);
        }

        // 2. The entities' names, for the labels of participants.
        Dictionary<string, string> entityNames = new Dictionary<string, string>();
        foreach (KeyValuePair<string, List<SampleRecord>> pair in statementsById)
        {
            if (SummaryBuilder.MostReliable(pair.Value).ReadRecord is EntityRecord entity && entity.Name != null)
            {
                entityNames.Add(pair.Key, entity.Name);
            }
        }

        // 3. One item per id.
        Dictionary<string, SampleItem> items = new Dictionary<string, SampleItem>();
        foreach (KeyValuePair<string, List<SampleRecord>> pair in statementsById)
        {
            SilRecord record = SummaryBuilder.MostReliable(pair.Value).ReadRecord!;
            SampleItem item = new SampleItem
            {
                Summary = SummaryBuilder.Build(pair.Value, entityNames), Words = WordsOf(record), Record = record, Statements = pair.Value,
            };
            items.Add(pair.Key, item);
        }
        return items;
    }

    /// <summary>The words keyword search reads: the name, title and body, in lower case, split at every non-letter, non-digit.</summary>
    private static HashSet<string> WordsOf(SilRecord record)
    {
        string text = (record as EntityRecord)?.Name + " " + record.Text?.Title + " " + record.Text?.Body;
        HashSet<string> words = new HashSet<string>();
        foreach (string word in Regex.Split(text.ToLowerInvariant(), "[^\\p{L}\\p{N}]+"))
        {
            if (word.Length > 0)
            {
                words.Add(word);
            }
        }
        return words;
    }
}
