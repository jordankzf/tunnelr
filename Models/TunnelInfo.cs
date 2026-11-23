using System.Text.Json.Serialization;
using Tunnelr.Services;

namespace Tunnelr.Models;

public class TunnelInfo
{
    public int Port { get; set; }
    public int RemotePort { get; set; }
    public string Nickname { get; set; } = "";
    public string Group { get; set; } = "";
    [JsonIgnore] public int EffectiveRemotePort => RemotePort == 0 ? Port : RemotePort;
    [JsonIgnore] public TunnelProcess Session { get; } = new();
}

public class ServerProfile
{
    public string Name { get; set; } = "Default";
    public string Server { get; set; } = "your.server.com";
    public int Port { get; set; } = 22;
    public string User { get; set; } = "root";
    public int HealthCheckInterval { get; set; } = 5;
    public bool Reconnect { get; set; }
    public List<TunnelInfo> Tunnels { get; set; } = new();
}

public class AppConfig : ServerProfile
{
    public List<ServerProfile> Profiles { get; set; } = new();
    public string? SelectedProfile { get; set; }
    [JsonIgnore] public IEnumerable<ServerProfile> AllProfiles => Profiles.Prepend(this);
    [JsonIgnore] public ServerProfile Current => Profiles.Find(p => p.Name == SelectedProfile) ?? this;
}
