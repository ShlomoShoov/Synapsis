using System.Text.Json;
using System.Text.Json.Serialization;

namespace Synapsis.Sil.Records;

/// <summary>
/// The body of <c>POST /v1/ingest</c>. <see cref="Records"/> stays raw JSON until each
/// record has been checked.
/// </summary>
public sealed record IngestBundle(
    [property: JsonPropertyName("sil")] string Sil,
    [property: JsonPropertyName("batch_id")] string BatchId,
    [property: JsonPropertyName("producer")] string Producer,
    [property: JsonPropertyName("package")] string Package,
    [property: JsonPropertyName("package_version")] int? PackageVersion,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("records")] IReadOnlyList<JsonElement> Records);
