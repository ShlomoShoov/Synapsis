using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Synapsis.Data.Models.Options;

namespace Synapsis.Data.Services;

/// <summary>
/// The service's lines in synapsis-pipeline-log (contracts/pipeline-log.md): the service adds to a few counters, and
/// one document is written at start, every 30 seconds, at stop and on an error (at most one error document per 30
/// seconds). A write that fails is logged here and dropped: the log never stops or slows the service.
/// </summary>
public sealed class PipelineLog : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(30);
    private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

    private readonly string _service;
    private readonly string _address;
    private readonly ILogger<PipelineLog> _logger;
    private long _processed;
    private long _written;
    private long _skipped;
    private long _failed;
    private long _lastErrorTicks;

    /// <summary>Creates the log of one service ("relay", "indexer" or "projector").</summary>
    public PipelineLog(string service, ElasticOptions options, ILogger<PipelineLog> logger)
    {
        _service = service;
        _address = options.Url.TrimEnd('/') + "/" + options.PipelineLogIndex + "/_doc";
        _logger = logger;
    }

    /// <summary>The Kafka messages still waiting for this consumer, when the service can tell cheaply.</summary>
    public Func<long?>? Lag { get; set; }

    /// <summary>Messages or records handled.</summary>
    public void Processed(long count = 1) => Interlocked.Add(ref _processed, count);

    /// <summary>What was written: messages published, documents indexed, records written to the graph.</summary>
    public void Written(long count = 1) => Interlocked.Add(ref _written, count);

    /// <summary>Handled with nothing to write.</summary>
    public void Skipped(long count = 1) => Interlocked.Add(ref _skipped, count);

    /// <summary>A failure (already logged by the service); also an error document, at most one per 30 seconds.</summary>
    public void Failed(string message)
    {
        Interlocked.Increment(ref _failed);
        long now = DateTime.UtcNow.Ticks;
        long last = Interlocked.Read(ref _lastErrorTicks);
        if (now - last >= Every.Ticks && Interlocked.CompareExchange(ref _lastErrorTicks, now, last) == last)
        {
            _ = WriteAsync("error", message, CancellationToken.None);
        }
    }

    /// <summary>Writes the start document, then a heartbeat every 30 seconds, and the stop document at shutdown.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await WriteAsync("start", null, CancellationToken.None);
        try
        {
            while (true)
            {
                await Task.Delay(Every, stoppingToken);
                await WriteAsync("heartbeat", null, CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
            await WriteAsync("stop", null, CancellationToken.None);
        }
    }

    private async Task WriteAsync(string logEvent, string? message, CancellationToken cancellationToken)
    {
        Dictionary<string, object?> document = new Dictionary<string, object?>
        {
            ["at"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            ["service"] = _service,
            ["event"] = logEvent,
            ["processed"] = Interlocked.Exchange(ref _processed, 0),
            ["written"] = Interlocked.Exchange(ref _written, 0),
            ["skipped"] = Interlocked.Exchange(ref _skipped, 0),
            ["failed"] = Interlocked.Exchange(ref _failed, 0)
        };
        try
        {
            long? lag = Lag?.Invoke();
            if (lag != null)
            {
                document["lag"] = lag;
            }
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "The lag could not be read.");
        }
        if (message != null)
        {
            document["message"] = message.Length > 500 ? message.Substring(0, 500) : message;
        }
        try
        {
            using StringContent content = new StringContent(JsonSerializer.Serialize(document), Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await Http.PostAsync(_address, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("The pipeline log refused a document: {Status}", (int)response.StatusCode);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning("The pipeline log could not be written: {Reason}", exception.Message);
        }
    }
}
