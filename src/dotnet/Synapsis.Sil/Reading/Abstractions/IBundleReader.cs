using Synapsis.Sil.Models.Dtos;
using Synapsis.Sil.Models.Results;

namespace Synapsis.Sil.Reading.Abstractions;

/// <summary>
/// Reads a request body into an ingest bundle (the batch envelope). The records inside stay raw JSON, to be checked
/// one by one.
/// </summary>
public interface IBundleReader
{
    /// <summary>The bundle; E-TOO-LARGE when it holds more than 5,000 records; E-BUNDLE when the text is not a bundle.</summary>
    Result<IngestBundle> Read(string json);
}
