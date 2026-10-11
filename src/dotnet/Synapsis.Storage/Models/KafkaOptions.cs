namespace Synapsis.Storage.Models;

/// <summary>
/// Where Kafka is, from the configuration section "Kafka" (environment variables Kafka__Bootstrap, Kafka__Topic).
/// </summary>
public sealed class KafkaOptions
{
    /// <summary>The bootstrap servers address, e.g. "localhost:9092".</summary>
    public string Bootstrap { get; set; } = "localhost:9092";

    /// <summary>The topic for accepted records, e.g. "ingest.accepted".</summary>
    public string Topic { get; set; } = "ingest.accepted";
}
