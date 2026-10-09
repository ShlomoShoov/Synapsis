using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Results;

namespace Synapsis.Sil.Reading.Abstractions;

/// <summary>
/// Reads a request body into an ingest bundle (the batch envelope). The records inside stay raw JSON, to be checked
/// one by one.
/// </summary>
public interface IBundleReader
{
    /// <summary>The bundle, or E-BUNDLE when the text is not one.</summary>
    Result<IngestBundle> Read(string json);
}
