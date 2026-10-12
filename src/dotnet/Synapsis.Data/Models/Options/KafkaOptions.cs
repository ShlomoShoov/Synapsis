namespace Synapsis.Data.Models.Options;

/// <summary>Connection settings for Kafka, from the configuration section "Kafka".</summary>
public sealed class KafkaOptions
{
    /// <summary>The Kafka bootstrap server list.</summary>
    public string Bootstrap { get; set; } = "localhost:9092";

    /// <summary>The default topic receiving accepted document IDs.</summary>
    public string Topic { get; set; } = "ingest.accepted";

    /// <summary>The consumer group used by a service consuming document IDs.</summary>
    public string GroupId { get; set; } = "synapsis-consumer";
}
