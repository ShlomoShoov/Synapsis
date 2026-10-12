namespace Synapsis.Data.Models.Options;

/// <summary>
/// Kafka's address and the topic of new record states, from the configuration section "Kafka" (sharedsettings.json;
/// a consumer's own appsettings.json adds its GroupId; environment variables such as Kafka__Bootstrap override both).
/// </summary>
public sealed class KafkaOptions
{
    /// <summary>The Kafka bootstrap server list.</summary>
    public string Bootstrap { get; set; } = string.Empty;

    /// <summary>The topic that receives the key of every new MongoDB document ("{id}|{source}|{state}").</summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>How many partitions the topic is created with.</summary>
    public int TopicPartitions { get; set; }

    /// <summary>How many copies of each partition Kafka keeps (1 with a single broker).</summary>
    public short TopicReplicationFactor { get; set; }

    /// <summary>The consumer group of a service that reads the topic; each consumer service has its own.</summary>
    public string GroupId { get; set; } = string.Empty;
}
