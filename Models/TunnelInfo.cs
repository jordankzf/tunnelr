using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Tunnelr.Models;

public class TunnelInfo
{
    public int Port { get; set; }
    public string Nickname { get; set; } = string.Empty;

    // Runtime only — not serialized
    [JsonIgnore]
    public bool IsActive { get; set; }

    [JsonIgnore]
    public Process? SshProcess { get; set; }
}

public class AppConfig
{
    public string Server { get; set; } = "your.server.com";
    public int Port { get; set; } = 22;
    public string User { get; set; } = "root";
    public List<TunnelInfo> Tunnels { get; set; } = new();
}
