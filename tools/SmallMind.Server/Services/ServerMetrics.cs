using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SmallMind.Server.Services;

public sealed class ServerMetrics
{
    private static readonly ActivitySource _activitySource = new("SmallMind.Server", "1.0.0");
    private static readonly Meter _meter = new("SmallMind.Server", "1.0.0");

    private readonly Counter<long> _requestsTotal;
    private readonly Histogram<double> _requestDuration;
    private readonly Counter<long> _tokensGenerated;
    private int _requestsInflight;

    public ServerMetrics()
    {
        _requestsTotal = _meter.CreateCounter<long>("requests_total", "requests", "Total number of requests");
        _requestDuration = _meter.CreateHistogram<double>("request_duration_ms", "ms", "Request duration in milliseconds");
        _tokensGenerated = _meter.CreateCounter<long>("tokens_generated_total", "tokens", "Total number of tokens generated");
        
        _meter.CreateObservableGauge("requests_inflight", () => _requestsInflight, "requests", "Current number of inflight requests");
    }

    public Activity? StartActivity(string name)
    {
        return _activitySource.StartActivity(name);
    }

    public void RecordRequest(string endpoint, double durationMs, int tokensGenerated, bool success)
    {
        var tags = new TagList
        {
            { "endpoint", endpoint },
            { "status", success ? "success" : "error" }
        };

        _requestsTotal.Add(1, tags);
        _requestDuration.Record(durationMs, tags);
        
        if (tokensGenerated > 0)
        {
            _tokensGenerated.Add(tokensGenerated, tags);
        }
    }

    public void IncrementInflight()
    {
        Interlocked.Increment(ref _requestsInflight);
    }

    public void DecrementInflight()
    {
        Interlocked.Decrement(ref _requestsInflight);
    }
}
