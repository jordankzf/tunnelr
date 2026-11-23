using System.Diagnostics;
using System.Threading;
using Tunnelr.Models;

namespace Tunnelr.Services;

// Called on the window dispatcher. Async continuations publish state on the same dispatcher.
public sealed class TunnelProcess : System.ComponentModel.INotifyPropertyChanged
{
    private CancellationTokenSource? _attempt;
    private Process? _child;
    private readonly Queue<string> _output = new();
    public string State { get; private set; } = "Off";
    public string Detail { get; private set; } = "";
    public string Output => string.Join(Environment.NewLine, _output);
    public bool Wanted { get; private set; }
    public event Action? Changed;
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private void Set(string state, string detail = "")
    {
        State = state;
        Detail = detail;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(null));
        Changed?.Invoke();
    }

    public void Start(TunnelInfo tunnel, ServerProfile profile)
    {
        if (_attempt != null) return;
        Wanted = true;
        _attempt = new CancellationTokenSource();
        _ = RunAsync(tunnel, profile, _attempt);
    }

    private async Task RunAsync(TunnelInfo tunnel, ServerProfile profile, CancellationTokenSource attempt)
    {
        var token = attempt.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                _output.Clear();
                Set("Connecting");
                var info = new ProcessStartInfo("ssh.exe")
                {
                    RedirectStandardError = true, RedirectStandardInput = true,
                    UseShellExecute = false, CreateNoWindow = true
                };
                foreach (var arg in new[] {
                    "-v", "-N", "-T", "-o", "BatchMode=yes", "-o", "StrictHostKeyChecking=yes",
                    "-o", "ExitOnForwardFailure=yes", "-o", "ControlMaster=no", "-o", "ControlPath=none",
                    "-o", "ServerAliveInterval=30", "-o", "ServerAliveCountMax=3", "-o", "ConnectTimeout=15",
                    "-L", $"127.0.0.1:{tunnel.Port}:localhost:{tunnel.EffectiveRemotePort}",
                    "-p", profile.Port.ToString(), "-l", profile.User, "--", profile.Server })
                    info.ArgumentList.Add(arg);
                using var process = Process.Start(info) ?? throw new IOException("OpenSSH could not start.");
                _child = process;
                process.StandardInput.Close();
                string? line;
                string failure = "";
                bool transient = false, unknownHost = false, changedKey = false, occupied = false;
                while ((line = await process.StandardError.ReadLineAsync()) != null)
                {
                    token.ThrowIfCancellationRequested();
                    if (_output.Count == 160) _output.Dequeue();
                    _output.Enqueue(line);
                    if (line == "debug1: Entering interactive session." && !process.HasExited)
                        Set("Listening");
                    if (line.StartsWith("debug", StringComparison.Ordinal)) continue;
                    if (!string.IsNullOrWhiteSpace(line)) failure = line;
                    occupied |= line.Contains("Address already in use", StringComparison.OrdinalIgnoreCase) ||
                                line.Contains("Permission denied", StringComparison.OrdinalIgnoreCase) && line.StartsWith("bind ");
                    changedKey |= line.Contains("REMOTE HOST IDENTIFICATION HAS CHANGED");
                    unknownHost |= line.Contains("host key is known for", StringComparison.OrdinalIgnoreCase);
                    transient |= IsNetworkFailure(line);
                }
                await process.WaitForExitAsync(token);
                token.ThrowIfCancellationRequested();
                _child = null;
                if (changedKey || unknownHost || occupied || !transient || !profile.Reconnect)
                {
                    Wanted = false;
                    if (changedKey) Set("Blocked", "Host key changed. Verify the change with the server administrator.");
                    else if (unknownHost) Set("Verify host", "Review the server fingerprint before connecting.");
                    else if (occupied) Set("Failed", $"Local port {tunnel.Port} is busy or unavailable.");
                    else Set("Failed", failure.Length > 0 ? failure : $"SSH exited with code {process.ExitCode}.");
                    return;
                }
                Set("Retrying", failure);
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(profile.HealthCheckInterval, 2, 60)), token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested) { Wanted = false; Set("Failed", ex.Message); }
        }
        finally
        {
            if (_attempt == attempt) { _attempt = null; _child = null; }
            attempt.Dispose();
        }
    }

    private static bool IsNetworkFailure(string line) =>
        new[] { "timed out", "reset by peer", "Connection reset", "Connection closed", "closed by remote host",
                "Broken pipe", "Network is unreachable", "Connection refused", "Could not resolve hostname" }
        .Any(reason => line.Contains(reason, StringComparison.OrdinalIgnoreCase));

    public void Stop(bool suspend = false)
    {
        bool resume = suspend && Wanted;
        _attempt?.Cancel();
        _attempt = null;
        if (_child != null)
        {
            try { if (!_child.HasExited) _child.Kill(true); }
            catch (InvalidOperationException) { }
            _child = null;
        }
        Wanted = resume;
        Set(resume ? "Paused" : "Off");
    }

    // OpenSSH owns the fingerprint prompt and writes the verified key to known_hosts.
    public static void VerifyHost(ServerProfile profile)
    {
        var info = new ProcessStartInfo("ssh.exe") { UseShellExecute = true };
        foreach (var arg in new[] { "-T", "-n", "-o", "StrictHostKeyChecking=ask", "-o", "BatchMode=no",
            "-o", "PreferredAuthentications=none", "-o", "ClearAllForwardings=yes", "-o", "ControlPath=none",
            "-o", "ConnectTimeout=15", "-p", profile.Port.ToString(), "-l", profile.User, "--", profile.Server, "exit" })
            info.ArgumentList.Add(arg);
        Process.Start(info)?.Dispose();
    }
}
