using System;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Synapsis.Data.Models.Options;

namespace Synapsis.Data.Dal;

/// <summary>The shared Kafka producer connection used by services that publish Synapsis events.</summary>
public sealed class KafkaContext : IDisposable
{
    private readonly IProducer<Null, string> _producer;

    /// <summary>Creates one idempotent producer for the process.</summary>
    public KafkaContext(KafkaOptions options)
    {
        ProducerConfig producerConfig = new ProducerConfig
        {
            BootstrapServers = options.Bootstrap,
            Acks = Acks.All,
            EnableIdempotence = true
        };
        _producer = new ProducerBuilder<Null, string>(producerConfig).Build();
    }

    /// <summary>Publishes a string value to a topic and waits for broker acknowledgement.</summary>
    public Task<DeliveryResult<Null, string>> ProduceAsync(string topic, string value, CancellationToken cancellationToken)
    {
        Message<Null, string> message = new Message<Null, string> { Value = value };
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
