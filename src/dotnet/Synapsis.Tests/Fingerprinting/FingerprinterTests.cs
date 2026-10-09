using System.Text.Json;
using Synapsis.Sil;
using Synapsis.Sil.Fingerprinting.Abstractions;
using Xunit;

namespace Synapsis.Tests.Fingerprinting;

/// <summary>Checks that the fingerprint changes with the content and only with the content.</summary>
public class FingerprinterTests
{
    /// <summary>The fingerprinter, through its abstraction.</summary>
    private static readonly IFingerprinter Fingerprinter = SilComponents.CreateFingerprinter();

    /// <summary>The base record the tests vary.</summary>
    private const string Quake = """{"kind": "event", "id": "evt:usgs:q1", "known_at": "2024-01-01T08:00:00Z", "known_at_basis": "published", "measures": [{"name": "magnitude", "value": 7.5, "unit": "magnitude"}]}""";

    /// <summary>The order of keys and the spacing do not change the fingerprint.</summary>
    [Fact]
    public void Key_order_and_spacing_do_not_matter()
    {
        string reordered = """
            { "measures": [ {"unit": "magnitude", "value": 7.5, "name": "magnitude"} ],
              "known_at_basis": "published", "id": "evt:usgs:q1", "kind": "event", "known_at": "2024-01-01T08:00:00Z" }
            """;
        Assert.Equal(Fingerprint(Quake), Fingerprint(reordered));
    }

    /// <summary>A different knowledge time is not a change of content.</summary>
    [Fact]
    public void Known_at_and_its_basis_are_left_out()
    {
        string knownLater = Quake.Replace("2024-01-01T08:00:00Z", "2025-06-01T00:00:00Z").Replace("published", "ingested");
        Assert.Equal(Fingerprint(Quake), Fingerprint(knownLater));
    }

    /// <summary>Any other change gives another fingerprint.</summary>
    [Fact]
    public void A_changed_value_changes_the_fingerprint()
    {
        Assert.NotEqual(Fingerprint(Quake), Fingerprint(Quake.Replace("7.5", "7.6")));
    }

    /// <summary>The order of items in a list is content: a reordered list is a change.</summary>
    [Fact]
    public void List_order_is_part_of_the_content()
    {
        string two = """{"kind": "event", "aliases": ["a", "b"]}""";
        string swapped = """{"kind": "event", "aliases": ["b", "a"]}""";
        Assert.NotEqual(Fingerprint(two), Fingerprint(swapped));
    }

    /// <summary>The fingerprint is 64 lower-case hex characters.</summary>
    [Fact]
    public void The_fingerprint_is_64_hex_characters()
    {
        string fingerprint = Fingerprint(Quake);
        Assert.Equal(64, fingerprint.Length);
        Assert.Equal(fingerprint.ToLowerInvariant(), fingerprint);
    }

    /// <summary>The fingerprint of a JSON text.</summary>
    private static string Fingerprint(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return Fingerprinter.OfRecord(document.RootElement);
    }
}
