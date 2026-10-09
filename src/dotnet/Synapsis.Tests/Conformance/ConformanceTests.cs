using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Synapsis.Sil.Validation;
using Synapsis.Sil.Validation.Batch;
using Synapsis.Tests.Support;
using Xunit;

namespace Synapsis.Tests.Conformance;

/// <summary>
/// The contract, in-process: every record case of tests/conformance/cases (files 01 to 06) runs through the C#
/// batch validator, with the registry of all real packages and an empty store, and must get exactly the answer the
/// case lists: the same records refused, with the same codes (and paths, where the case gives one), and the same
/// number of duplicates. The comparison is the one tests/conformance/run.py makes over HTTP.
/// </summary>
public class ConformanceTests
{
    /// <summary>Every record case, as (file, case name), one test each.</summary>
    public static IEnumerable<object[]> AllRecordCases()
    {
        List<object[]> cases = new List<object[]>();
        foreach (string path in CaseFiles())
        {
            JsonElement file = Parse(File.ReadAllText(path));
            foreach (JsonElement testCase in file.GetProperty("cases").EnumerateArray())
            {
                if (testCase.TryGetProperty("records", out JsonElement _))
                {
                    cases.Add(new object[] { Path.GetFileNameWithoutExtension(path), testCase.GetProperty("name").GetString()! });
                }
            }
        }
        return cases;
    }

    /// <summary>One case gives exactly the answer the contract lists.</summary>
    [Theory]
    [MemberData(nameof(AllRecordCases))]
    public async Task The_case_gets_the_contract_answer(string file, string caseName)
    {
        JsonElement testCase = FindCase(file, caseName);
        List<JsonElement> records = new List<JsonElement>();
        foreach (JsonElement record in testCase.GetProperty("records").EnumerateArray())
        {
            records.Add(record);
        }
        BatchResult result = await BatchValidator.ValidateAsync(records, TestRegistry.All, new FakeResolver(), DateTime.UtcNow);
        List<string> problems = Compare(testCase, result);
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>The differences between the expected and the actual answer (empty when they agree).</summary>
    private static List<string> Compare(JsonElement testCase, BatchResult result)
    {
        List<string> problems = new List<string>();
        Dictionary<int, Rejection> actual = new Dictionary<int, Rejection>();
        foreach (Rejection rejection in result.Rejected)
        {
            actual[rejection.Index] = rejection;
        }
        HashSet<int> expectedIndexes = new HashSet<int>();
        foreach (JsonElement expected in testCase.GetProperty("rejected").EnumerateArray())
        {
            int index = expected.GetProperty("index").GetInt32();
            expectedIndexes.Add(index);
            CompareOne(expected, index, actual, problems);
        }
        foreach (Rejection rejection in result.Rejected)
        {
            if (!expectedIndexes.Contains(rejection.Index))
            {
                problems.Add($"record {rejection.Index}: expected to be accepted, got {rejection.Code} ({rejection.Path}: {rejection.Message})");
            }
        }
        int expectedDuplicates = 0;
        if (testCase.TryGetProperty("duplicates", out JsonElement duplicates))
        {
            expectedDuplicates = duplicates.GetInt32();
        }
        if (result.Duplicates != expectedDuplicates)
        {
            problems.Add($"expected {expectedDuplicates} duplicates, got {result.Duplicates}");
        }
        return problems;
    }

    /// <summary>One expected refusal: the record is refused, with the code, and with the path when the case gives one.</summary>
    private static void CompareOne(JsonElement expected, int index, Dictionary<int, Rejection> actual, List<string> problems)
    {
        string code = expected.GetProperty("code").GetString()!;
        if (!actual.TryGetValue(index, out Rejection? rejection))
        {
            problems.Add($"record {index}: expected {code}, but it was accepted");
            return;
        }
        if (rejection.Code != code)
        {
            problems.Add($"record {index}: expected {code}, got {rejection.Code} ({rejection.Path}: {rejection.Message})");
            return;
        }
        if (expected.TryGetProperty("path", out JsonElement path) && path.GetString() != rejection.Path)
        {
            problems.Add($"record {index}: {code} expected at '{path.GetString()}', reported at '{rejection.Path}'");
        }
    }

    /// <summary>The case with this name in this file.</summary>
    private static JsonElement FindCase(string file, string caseName)
    {
        JsonElement cases = Parse(RepositoryFiles.ReadText($"tests/conformance/cases/{file}.json")).GetProperty("cases");
        foreach (JsonElement testCase in cases.EnumerateArray())
        {
            if (testCase.GetProperty("name").GetString() == caseName)
            {
                return testCase;
            }
        }
        throw new KeyNotFoundException($"case '{caseName}' in {file}");
    }

    /// <summary>The case files 01 to 06 (07 holds request-level cases, which need HTTP).</summary>
    private static List<string> CaseFiles()
    {
        List<string> files = new List<string>();
        foreach (string path in Directory.GetFiles(RepositoryFiles.PathOf("tests/conformance/cases"), "*.json"))
        {
            if (!Path.GetFileName(path).StartsWith("07"))
            {
                files.Add(path);
            }
        }
        files.Sort(string.CompareOrdinal);
        return files;
    }

    /// <summary>Parses JSON text into an element that stays valid after parsing.</summary>
    private static JsonElement Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
