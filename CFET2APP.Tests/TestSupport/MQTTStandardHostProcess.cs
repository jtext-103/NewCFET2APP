using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

namespace Jtext103.CFET2.CFET2App.Tests.TestSupport;

internal sealed class MQTTStandardHostProcess : IAsyncDisposable
{
    private readonly Process process;
    private readonly Task<string> output, error;
    private bool booted;
    private readonly string directory;
    private readonly HttpClient http = new(new HttpClientHandler { UseProxy = false });
    public string Origin { get; }
    public int WebSocketOffset { get; }
    public static int FreePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }
    private MQTTStandardHostProcess(Process process, string directory, int port, int wsOffset)
    {
        this.process = process; this.directory = directory; Origin = "http://127.0.0.1:" + port; WebSocketOffset = wsOffset;
        output = CaptureOutput(); error = process.StandardError.ReadToEndAsync();
        http.Timeout = TimeSpan.FromSeconds(2); http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
    private async Task<string> CaptureOutput()
    {
        var text = new System.Text.StringBuilder(); string? line;
        while ((line = await process.StandardOutput.ReadLineAsync()) != null)
        {
            text.AppendLine(line);
            if (line.Contains("Cfet2 host running without CLI", StringComparison.Ordinal)) Volatile.Write(ref booted, true);
        }
        return text.ToString();
    }
    public async Task WaitStarted(CancellationToken ct)
    {
        while (!Volatile.Read(ref booted)) { await CheckRunning(); await Task.Delay(20, ct); }
    }
    public static async Task<MQTTStandardHostProcess> Start(int brokerPort, bool publishMqtt, bool publishWs, bool invalid = false, int? wsOffset = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), "cfet-mqtt-standard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); Directory.CreateDirectory(Path.Combine(directory, "thingConfig", "device"));
        Directory.CreateDirectory(Path.Combine(directory, "CustomView"));
        await File.WriteAllTextAsync(Path.Combine(directory, "CustomView", "CustomViewConfig.json"), "{\"RegularMatches\":{}}");
        var app = Path.Combine(AppContext.BaseDirectory, "StandardHost");
        var fixture = Path.Combine(AppContext.BaseDirectory, "HostFixture", "CFET2APP.TestFixtures.dll");
        if (!File.Exists(Path.Combine(app, "CFET2APP.runtimeconfig.json")) || !File.Exists(fixture))
            throw new FileNotFoundException("Standard host test assets are missing. Build CFET2APP.Tests before running integration tests.");
        foreach (var file in Directory.GetFiles(app))
            File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
        File.Copy(fixture, Path.Combine(directory, Path.GetFileName(fixture)));
        await File.WriteAllTextAsync(Path.Combine(directory, "thingConfig", "device", "Config.json"), new JObject { ["Type"] = "StandardHostDevice" }.ToString());
        var port = FreePort(); var wsPort = FreePort(); while (wsPort <= port) { port = FreePort(); wsPort = FreePort(); }
        var offset = wsOffset ?? wsPort - port;
        var transports = new JArray(); if (publishWs) transports.Add("WebSocket"); if (publishMqtt) transports.Add("Mqtt");
        var config = new JObject { ["IP"] = "http://127.0.0.1", ["Port"] = port.ToString(), ["Accept"] = "Json",
            ["EventPublishTransports"] = transports, ["HttpEventSubscriptionTransport"] = "Mqtt", ["WebSocketPortOffset"] = offset,
            ["MqttBrokerHost"] = "127.0.0.1", ["MqttBrokerPort"] = invalid ? 0 : brokerPort, ["MqttOperationTimeoutSeconds"] = 1 };
        await File.WriteAllTextAsync(Path.Combine(directory, "GlobalConfig.json"), config.ToString());
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, UseShellExecute = false,
            CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(directory, "CFET2APP.dll")); start.ArgumentList.Add("--no-cli");
        return new(Process.Start(start)!, directory, port, offset);
    }
    public async Task<JToken> Read(string path, CancellationToken ct)
    {
        await CheckRunning();
        using var response = await http.GetAsync(Origin + path, ct); response.EnsureSuccessStatusCode();
        var sample = JObject.Parse(await response.Content.ReadAsStringAsync(ct));
        if (sample["CFET2CORE_SAMPLE_ISVALID"]?.Value<bool>() != true) throw new InvalidOperationException("Invalid HTTP sample: " + sample);
        return sample["CFET2CORE_SAMPLE_VAL"]!;
    }
    public async Task<JObject> WaitStatus(Func<JObject, bool> predicate, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested(); await CheckRunning();
            try { var status = (JObject)await Read("/MQTTTCM/status", ct); if (predicate(status)) return status; }
            catch (HttpRequestException) { }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { await CheckRunning(); }
            await Task.Delay(30, ct);
        }
    }
    public async Task Publish(int value, CancellationToken ct)
    {
        await CheckRunning();
        using var response = await http.PutAsync(Origin + "/device/Publish", new StringContent("[" + value + "]", System.Text.Encoding.UTF8, "application/json"), ct);
        response.EnsureSuccessStatusCode();
        var result = JObject.Parse(await response.Content.ReadAsStringAsync(ct));
        if (result["CFET2CORE_SAMPLE_ISVALID"]?.Value<bool>() != true) throw new InvalidOperationException("Publish failed: " + result);
    }
    private async Task CheckRunning()
    {
        if (process.HasExited) throw new InvalidOperationException("Standard APP exited " + process.ExitCode + ": " + await error + await output);
    }
    public async Task AssertRejected(CancellationToken ct)
    {
        await process.WaitForExitAsync(ct);
        if (process.ExitCode == 0) throw new InvalidOperationException("Invalid configuration must terminate startup.");
        var logs = await error + await output;
        if (!logs.Contains("brokerPort", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Missing configuration failure: " + logs);
        if (logs.Contains("Now listening on", StringComparison.Ordinal)) throw new InvalidOperationException("HTTP started before configuration rejection.");
    }
    public async ValueTask DisposeAsync()
    {
        if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        await File.WriteAllTextAsync(Path.Combine(directory, "process.log"), await output + await error);
        Console.WriteLine("Standard APP logs: " + directory); http.Dispose(); process.Dispose();
    }
}
