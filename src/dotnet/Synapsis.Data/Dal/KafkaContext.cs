using System;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Synapsis.Data.Models.Options;

namespace Synapsis.Data.Dal;

/// <summary>The shared Kafka producer connection used by services that publish Synapsis events.</summary>
public sealed class KafkaContext : IDisposable
{
    private readonly IProducer<string, string> _producer;

    /// <summary>Creates one idempotent producer for the process.</summary>
    public KafkaContext(KafkaOptions options)
    {
        ProducerConfig producerConfig = new ProducerConfig
        {
            BootstrapServers = options.Bootstrap,
            Acks = Acks.All,
            EnableIdempotence = true
        };
        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
    }

    /// <summary>Publishes a document id under its record id and waits for broker acknowledgement.</summary>
    public Task<DeliveryResult<string, string>> ProduceAsync(string topic, string recordId, string documentId, CancellationToken cancellationToken)
    {
        Message<string, string> message = new Message<string, string> { Key = recordId, Value = documentId };
        return _producer.ProduceAsync(topic, message, cancellationToken);
    }

    /// <summary>Flushes and disposes the producer.</summary>
    public void Dispose()
    {
        try
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
        }
        finally
        {
            _producer.Dispose();
        }
    }
}
