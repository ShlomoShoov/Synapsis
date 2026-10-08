using System.Text.Json.Nodes;

namespace Synapsis.Sil.Registry;

/// <summary>
/// One definition in the effective registry, as stored in <c>registry_entries</c>:
/// <c>_id</c> is <see cref="Key"/>, e.g. <c>event_type:CYB.VULN.KNOWN_EXPLOITED</c>.
/// </summary>
public sealed record RegistryEntry(
    string Key,
    string Package,
    int SinceVersion,
    JsonObject Definition,
    bool Deprecated);
