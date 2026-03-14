using System.IO;
using System.Text.Json;
using Tunnelr.Models;

namespace Tunnelr.Services;

public static class TunnelConfig
{
    private static readonly string ConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "tunnels.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static bool _wasFirstRun;

    public static bool ConfigExists() => !_wasFirstRun;

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            _wasFirstRun = true;
            return CreateDefaults();
        }

        var json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? CreateDefaults();
    }

    public static void Save(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    private static AppConfig CreateDefaults()
    {
        return new AppConfig
        {
            Tunnels = new List<TunnelInfo>
            {
                new() { Port = 5432, Nickname = "PostgreSQL" },
                new() { Port = 3306, Nickname = "MySQL" },
                new() { Port = 6379, Nickname = "Redis" },
                new() { Port = 8080, Nickname = "Web App" },
                new() { Port = 27017, Nickname = "MongoDB" },
                new() { Port = 9090, Nickname = "Prometheus" }
            }
        };
    }
}
