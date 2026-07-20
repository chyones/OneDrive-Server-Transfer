using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OneDriveServerTransfer.SourceResolution;

namespace OneDriveServerTransfer.Tests.TestSupport;

/// <summary>Programmable Graph request channel double for resolver tests.</summary>
internal sealed class FakeGraphRequestChannel : IGraphRequestChannel
{
    public Func<Uri, string, CancellationToken, Task<JsonDocument>>? Handler { get; set; }

    public List<(Uri Uri, string Template)> Requests { get; } = [];

    public Task<JsonDocument> GetJsonAsync(Uri requestUri, string endpointTemplate, CancellationToken cancellationToken)
    {
        Requests.Add((requestUri, endpointTemplate));
        return Handler?.Invoke(requestUri, endpointTemplate, cancellationToken)
            ?? throw new InvalidOperationException("Handler not configured.");
    }

    public static JsonDocument Json(string json) => JsonDocument.Parse(json);
}

/// <summary>HttpClientFactory over a stub handler for channel tests.</summary>
internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

/// <summary>Captures rendered log messages for redaction and correlation assertions.</summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Messages.Add(formatter(state, exception));
}
