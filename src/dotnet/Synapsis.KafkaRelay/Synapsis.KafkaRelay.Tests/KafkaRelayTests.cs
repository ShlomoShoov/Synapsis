using Confluent.Kafka;
using Synapsis.Data.Models.Options;
using Synapsis.Data.Dal;
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

        KafkaOptions options = new KafkaOptions { Bootstrap = bootstrap, Topic = topic };
        using KafkaContext producer = new KafkaContext(options);
        using IConsumer<string, string> consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrap,
            GroupId = $"synapsis-kafka-relay-test-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();

        string recordId = "ent:test:record";
        DeliveryResult<string, string> delivery = await producer.ProduceAsync(topic, recordId, documentId, default);
        Assert.Equal(topic, delivery.Topic);

        consumer.Subscribe(topic);
        ConsumeResult<string, string> received = consumer.Consume(TimeSpan.FromSeconds(10));
        Assert.Equal(recordId, received.Message.Key);
        Assert.Equal(documentId, received.Message.Value);
        consumer.Close();
    }
}
