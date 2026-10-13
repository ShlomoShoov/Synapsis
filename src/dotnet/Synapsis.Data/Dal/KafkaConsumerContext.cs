using System;
using System.Threading;
using Confluent.Kafka;
using Synapsis.Data.Models.Options;

namespace Synapsis.Data.Dal;

/// <summary>Owns one Kafka consumer and commits offsets only when the caller asks.</summary>
public sealed class KafkaConsumerContext : IDisposable
{
    private readonly IConsumer<Ignore, string> _consumer;

    /// <summary>Creates a manual-commit consumer for the configured topic and group.</summary>
    public KafkaConsumerContext(KafkaOptions options)
    {
        ConsumerConfig consumerConfig = new ConsumerConfig
        {
            BootstrapServers = options.Bootstrap,
            GroupId = options.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        };
        _consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
        _consumer.Subscribe(options.Topic);
    }

    /// <summary>Waits for the next message or throws when Kafka reports a failure.</summary>
    public ConsumeResult<Ignore, string> Consume(CancellationToken cancellationToken)
    {
        return _consumer.Consume(cancellationToken);
    }

    /// <summary>Commits the offset for a message after successful processing.</summary>
    public void Commit(ConsumeResult<Ignore, string> message)
    {
        _consumer.Commit(message);
    }

    /// <summary>
    /// The messages still waiting in the partitions this consumer holds: the last known end of each partition minus
    /// its position (both kept by the client, no request); null before the first assignment.
    /// </summary>
    public long? Lag()
    {
        long lag = 0;
        bool known = false;
        foreach (TopicPartition partition in _consumer.Assignment)
        {
            WatermarkOffsets ends = _consumer.GetWatermarkOffsets(partition);
            Offset position = _consumer.Position(partition);
            if (ends.High == Offset.Unset)
            {
                continue;
            }
            lag += position == Offset.Unset ? ends.High.Value - Math.Max(0, ends.Low.Value) : Math.Max(0, ends.High.Value - position.Value);
            known = true;
        }
        return known ? lag : null;
    }

    /// <summary>Closes the consumer and releases its Kafka connection.</summary>
    public void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();
    }
}
