using System.Text.Json;
using Synapsis.Sil.Models.Results;
namespace Synapsis.Sil.Services.Rules.Envelope;

/// <summary>
/// Rule 1: the record's "kind" must be entity, event, relation or conclusion. A source is declared in a schema
/// package, never sent as a record. Every later rule depends on the kind, so this runs first.
/// </summary>
public sealed class KindRule : IEnvelopeRule
{
    /// <summary>Passes for the four kinds; E-KIND at "kind" otherwise.</summary>
    public Result Check(JsonElement raw)
    {
        string kind = KindText.Of(raw);
        if (kind == "entity" || kind == "event" || kind == "relation" || kind == "conclusion")
        {
            return Result.Success();
        }
        return Result.Failure(ErrorCodes.Kind, "kind",
            "kind must be entity, event, relation or conclusion; sources are declared in schema packages");
    }
}
