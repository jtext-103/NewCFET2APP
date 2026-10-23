using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Jtext103.CFET2.CFET2App.Tests.TestSupport;

internal sealed class MQTTIsolatedBroker(string executable, string directory, int port) : IDisposable
{
    private Process? process;
    private Task<string>? output, error;
    private int runs;
    public int Port { get; } = port;
    public static async Task<MQTTIsolatedBroker> Create(string settings, CancellationToken token)
    {
        var executable = Environment.GetEnvironmentVariable("CFET_MOSQUITTO_EXE") ?? "D:/install/mosquitto/mosquitto.exe";
        if (!File.Exists(executable)) Assert.Inconclusive("Set CFET_MOSQUITTO_EXE to Mosquitto.");
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start(); var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        var directory = Path.Combine(Path.GetTempPath(), "cfet-mqtt-subscriptions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "mosquitto.conf"), $"listener {port} 127.0.0.1\npersistence false\n" + settings, token);
        var broker = new MQTTIsolatedBroker(executable, directory, port);
        try { await broker.Start(token); return broker; } catch { broker.Dispose(); throw; }
    }
    public async Task Start(CancellationToken token)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-c"); start.ArgumentList.Add(Path.Combine(directory, "mosquitto.conf")); start.ArgumentList.Add("-v");
        process = Process.Start(start)!; output = process.StandardOutput.ReadToEndAsync(); error = process.StandardError.ReadToEndAsync(); runs++;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (process.HasExited) throw new InvalidOperationException("Isolated broker exited: " + await error);
            using var probe = new TcpClient();
            try { await probe.ConnectAsync(IPAddress.Loopback, Port, token); return; }
            catch (SocketException) { await Task.Delay(30, token); }
        }
    }
    public void Stop()
    {
        if (process == null) return;
        if (!process.HasExited) { process.Kill(); process.WaitForExit(3000); }
        File.WriteAllText(Path.Combine(directory, $"broker-{runs}.log"), error!.GetAwaiter().GetResult() + output!.GetAwaiter().GetResult());
        process.Dispose(); process = null;
    }
    public void Dispose() { Stop(); Console.WriteLine("Isolated subscriptions broker: " + directory); }
}
