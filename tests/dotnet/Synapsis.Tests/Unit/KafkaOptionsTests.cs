using Synapsis.Data.Models.Options;
using Xunit;

namespace Synapsis.Tests.Unit;

/// <summary>Checks the Kafka settings used by the independent consumers.</summary>
public sealed class KafkaOptionsTests
{
    /// <summary>Checks the default topic and group names.</summary>
    [Fact]
    public void Kafka_options_have_the_accepted_topic_and_consumer_group_defaults()
    {
        KafkaOptions options = new();

        Assert.Equal("ingest.accepted", options.Topic);
        Assert.Equal("synapsis-consumer", options.GroupId);
    }
}
