using Confluent.Kafka;
using Synapsis.Storage.Models;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Synapsis.KafkaRelay.Tests;

public sealed class KafkaRelayTests
{
    [Fact]
    public void KafkaOptions_HaveUsableDevelopmentDefaults()
    {
        KafkaOptions options = new();

        Assert.Equal("localhost:9092", options.Bootstrap);
        Assert.Equal("ingest.accepted", options.Topic);
    }

    [Fact]
    public async Task KafkaSmoke_ProducesAndConsumesTheSameDocumentId()
    {
        string? runSmoke = Environment.GetEnvironmentVariable("SYNAPSIS_RUN_KAFKA_SMOKE");
        if (!string.Equals(runSmoke, "1", StringComparison.Ordinal))
        {
            return;
        }

        string bootstrap = Environment.GetEnvironmentVariable("SYNAPSIS_KAFKA_BOOTSTRAP") ?? "localhost:9092";
        string topic = $"synapsis.kafka-relay.test.{Guid.NewGuid():N}";
        string documentId = $"smoke-{Guid.NewGuid():N}";

        using IProducer<Null, string> producer = new ProducerBuilder<Null, string>(
            new ProducerConfig { BootstrapServers = bootstrap, Acks = Acks.All }).Build();
        using IConsumer<Ignore, string> consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrap,
            GroupId = $"synapsis-kafka-relay-test-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();

        DeliveryResult<Null, string> delivery = await producer.ProduceAsync(topic, new Message<Null, string> { Value = documentId });
        Assert.Equal(topic, delivery.Topic);

        consumer.Subscribe(topic);
        ConsumeResult<Ignore, string> received = consumer.Consume(TimeSpan.FromSeconds(10));
        Assert.Equal(documentId, received.Message.Value);
        consumer.Close();
    }
}
