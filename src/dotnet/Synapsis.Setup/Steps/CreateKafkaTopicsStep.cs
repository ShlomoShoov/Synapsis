using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Synapsis.Data.Models.Options;

namespace Synapsis.Setup;

/// <summary>
/// Creates the topic of new record states with its partitions. Kafka's automatic topic creation is off (Compose), so the
/// topic exists only through this step.
/// </summary>
public sealed class CreateKafkaTopicsStep : ISetupStep
{
    private readonly KafkaOptions _kafkaOptions;

    /// <summary>Creates the step over the Kafka settings (address, topic, partitions, copies).</summary>
    public CreateKafkaTopicsStep(KafkaOptions kafkaOptions)
    {
        _kafkaOptions = kafkaOptions;
    }

    /// <summary>
    /// Creates the topic when it does not exist. An existing topic is left as it is, even with another number of
    /// partitions. The admin client is only needed here, so it is made and closed in this step.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        AdminClientConfig config = new AdminClientConfig { BootstrapServers = _kafkaOptions.Bootstrap };
        using IAdminClient adminClient = new AdminClientBuilder(config).Build();
        TopicSpecification topic = new TopicSpecification
        {
            Name = _kafkaOptions.Topic,
            NumPartitions = _kafkaOptions.TopicPartitions,
            ReplicationFactor = _kafkaOptions.TopicReplicationFactor
        };
        try
        {
            await adminClient.CreateTopicsAsync(new[] { topic });
        }
        catch (CreateTopicsException problem) when (problem.Results[0].Error.Code == ErrorCode.TopicAlreadyExists)
        {
            // The topic exists: nothing to do.
        }
    }
}
