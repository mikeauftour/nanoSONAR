using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace NanoSonarController;

// Best-effort SteelSeries GG UI refresh. The Sonar REST write remains authoritative.
internal sealed class SonarLiveFeedback : IDisposable
{
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private ClientWebSocket? socket;
    private Uri? endpoint;
    private static readonly Dictionary<string, string> Channels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["master"] = "MASTER", ["game"] = "GAME", ["chatRender"] = "CHAT",
        ["media"] = "MEDIA", ["aux"] = "AUX", ["chatCapture"] = "MIC"
    };

    public void Configure(string ggEncryptedAddress)
    {
        var uri = new Uri("wss://" + ggEncryptedAddress + "/eventing");
        if (!uri.IsLoopback) throw new InvalidOperationException("GG eventing muss lokal sein.");
        if (endpoint != uri)
        {
            socket?.Dispose();
            socket = null;
            endpoint = uri;
        }
    }

    public Task Volume(string mix, string channel, double value) =>
        Send(mix, channel, "VOLUME", Math.Clamp(value, 0, 1) * 100.0);

    public Task Mute(string mix, string channel, bool muted) =>
        Send(mix, channel, "MUTE", muted);

    private async Task Send(string mix, string channel, string action, object value)
    {
        if (endpoint is null || !Channels.TryGetValue(channel, out var name) ||
            (mix != "streaming" && mix != "monitoring")) return;
        var prefix = mix == "streaming" ? "STREAMING" : "MONITOR";
        var payload = JsonSerializer.Serialize(new
        {
            @event = "EVENT_SUB_APP_ACTIONS_DATA_CHANGED",
            data = new
            {
                SubAppName = "sonar",
                Id = Guid.Empty.ToString(),
                ActionName = $"{prefix}_{name}_{action}",
                Value = value
            }
        });
        await sendLock.WaitAsync();
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    if (socket?.State != WebSocketState.Open)
                    {
                        socket?.Dispose();
                        socket = new ClientWebSocket();
                        socket.Options.RemoteCertificateValidationCallback = (_, _, _, errors) =>
                            endpoint.IsLoopback;
                        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                        await socket.ConnectAsync(endpoint, timeout.Token);
                    }
                    using var sendTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(payload)),
                        WebSocketMessageType.Text, true, sendTimeout.Token);
                    return;
                }
                catch
                {
                    socket?.Dispose();
                    socket = null;
                    // Eventing is optional: never fail the successful Sonar REST write.
                }
            }
        }
        finally { sendLock.Release(); }
    }

    public void Dispose()
    {
        socket?.Dispose();
        sendLock.Dispose();
    }
}
