using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Tunnelr.Models;

public class TunnelInfo
{
    public int Port { get; set; }
    public int RemotePort { get; set; }
    public string Nickname { get; set; } = string.Empty;

    // Runtime only — not serialized
    [JsonIgnore]
    public bool IsActive { get; set; }

    [JsonIgnore]
    public bool HasError { get; set; }

    [JsonIgnore]
    public string? ErrorMessage { get; set; }

    [JsonIgnore]
    public Process? SshProcess { get; set; }

    /// <summary>Effective remote port — falls back to local port if not set.</summary>
    [JsonIgnore]
    public int EffectiveRemotePort => RemotePort > 0 ? RemotePort : Port;
}

public class AppConfig
{
    public string Server { get; set; } = "your.server.com";
    public int Port { get; set; } = 22;
    public string User { get; set; } = "root";
    public int HealthCheckInterval { get; set; } = 5;
    public List<TunnelInfo> Tunnels { get; set; } = new();
}
