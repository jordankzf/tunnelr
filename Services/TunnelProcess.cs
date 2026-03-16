using System.Diagnostics;
using System.Text;
using Tunnelr.Models;

namespace Tunnelr.Services;

public static class TunnelProcess
{
    public static bool Start(TunnelInfo tunnel, AppConfig config)
    {
        if (tunnel.IsActive && tunnel.SshProcess != null && !tunnel.SshProcess.HasExited)
            return true;

        tunnel.HasError = false;
        tunnel.ErrorMessage = null;

        var args = string.Join(" ",
            "-L", $"{tunnel.Port}:localhost:{tunnel.EffectiveRemotePort}",
            $"{config.User}@{config.Server}",
            "-p", config.Port.ToString(),
            "-N",
            "-o", "StrictHostKeyChecking=no",
            "-o", "ExitOnForwardFailure=yes",
            "-o", "ServerAliveInterval=30",
            "-o", "ServerAliveCountMax=3"
        );

        var psi = new ProcessStartInfo
        {
            FileName = "ssh",
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };

        try
        {
            var process = Process.Start(psi);
            if (process == null)
            {
                tunnel.HasError = true;
                tunnel.ErrorMessage = "Failed to start ssh process.";
                return false;
            }

            // Capture stderr asynchronously
            var stderr = new StringBuilder();
            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null) stderr.AppendLine(e.Data);
            };
            process.BeginErrorReadLine();

            // Store stderr ref for later retrieval
            tunnel.SshProcess = process;
            tunnel.IsActive = true;
            _stderrBuffers[tunnel] = stderr;
            return true;
        }
        catch (Exception ex)
        {
            tunnel.HasError = true;
            tunnel.ErrorMessage = ex.Message;
            return false;
        }
    }

    private static readonly Dictionary<TunnelInfo, StringBuilder> _stderrBuffers = new();

    public static void Stop(TunnelInfo tunnel)
    {
        if (tunnel.SshProcess != null)
        {
            try
            {
                if (!tunnel.SshProcess.HasExited)
                    tunnel.SshProcess.Kill(true);
            }
            catch { }
            finally
            {
                tunnel.SshProcess.Dispose();
                tunnel.SshProcess = null;
            }
        }
        _stderrBuffers.Remove(tunnel);
        tunnel.IsActive = false;
        tunnel.HasError = false;
        tunnel.ErrorMessage = null;
    }

    /// <summary>Gets captured stderr for a tunnel whose process has exited.</summary>
    public static string? GetError(TunnelInfo tunnel)
    {
        if (_stderrBuffers.TryGetValue(tunnel, out var buf))
        {
            var msg = buf.ToString().Trim();
            return string.IsNullOrEmpty(msg) ? null : msg;
        }
        return null;
    }

    public static void StopAll(IEnumerable<TunnelInfo> tunnels)
    {
        foreach (var tunnel in tunnels)
            Stop(tunnel);
    }
}
