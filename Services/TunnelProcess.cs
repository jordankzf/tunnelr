using System.Diagnostics;
using Tunnelr.Models;

namespace Tunnelr.Services;

public static class TunnelProcess
{
    public static bool Start(TunnelInfo tunnel, AppConfig config)
    {
        if (tunnel.IsActive && tunnel.SshProcess != null && !tunnel.SshProcess.HasExited)
            return true;

        var args = string.Join(" ",
            "-L", $"{tunnel.Port}:localhost:{tunnel.Port}",
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
            if (process == null) return false;

            tunnel.SshProcess = process;
            tunnel.IsActive = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

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
        tunnel.IsActive = false;
    }

    public static void StopAll(IEnumerable<TunnelInfo> tunnels)
    {
        foreach (var tunnel in tunnels)
            Stop(tunnel);
    }
}
