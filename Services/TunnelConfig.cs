using System.IO;
using System.Text.Json;
using Tunnelr.Models;

namespace Tunnelr.Services;

public static class TunnelConfig
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tunnelr");
    public static string ConfigPath => Path.Combine(ConfigDirectory, "tunnels.json");
    public static string BackupPath => ConfigPath + ".previous";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // A tray instance can stay open after its window closes. Keep it the sole writer.
    public static FileStream AcquireLock()
    {
        Directory.CreateDirectory(ConfigDirectory);
        return File.Open(Path.Combine(ConfigDirectory, "tunnels.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
    }

    public static AppConfig Load(out bool firstRun)
    {
        firstRun = false;
        try { return Read(ConfigPath); }
        catch (FileNotFoundException) { }

        AppConfig legacy;
        try { legacy = Read(Path.Combine(AppContext.BaseDirectory, "tunnels.json")); }
        catch (FileNotFoundException)
        {
            firstRun = true;
            return CreateDefaults();
        }

        return legacy;
    }

    public static AppConfig Read(string path)
    {
        using var file = File.OpenRead(path);
        try
        {
            using var document = JsonDocument.Parse(file);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("server", out _) ||
                !root.TryGetProperty("tunnels", out _)) throw new JsonException("Missing server or tunnels.");
            var config = root.Deserialize<AppConfig>(JsonOptions)!;
            Validate(config);
            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Could not read settings from {path}.", ex);
        }
    }

    public static void Validate(AppConfig config)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (config.Profiles == null) throw new InvalidDataException("Invalid server profiles.");
        foreach (var profile in config.AllProfiles)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.Name) || !names.Add(profile.Name) ||
                string.IsNullOrWhiteSpace(profile.Server) || profile.Server.StartsWith('-') ||
                profile.Server.Any(char.IsWhiteSpace) || string.IsNullOrWhiteSpace(profile.User) ||
                profile.Port is < 1 or > 65535 || profile.Tunnels == null)
                throw new InvalidDataException("Check the server address, SSH port, and unique profile name.");
            var ports = new HashSet<int>();
            foreach (var t in profile.Tunnels)
                if (t == null || t.Port is < 1 or > 65535 || t.RemotePort is < 0 or > 65535 ||
                    string.IsNullOrWhiteSpace(t.Nickname) || !ports.Add(t.Port))
                    throw new InvalidDataException($"Check tunnel names and ports in {profile.Name}.");
        }
    }

    public static void Save(AppConfig config)
    {
        Validate(config);
        var temporaryPath = Path.Combine(ConfigDirectory, Path.GetRandomFileName());
        try
        {
            using (var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(file, config, JsonOptions);
                file.Flush(flushToDisk: true);
            }
            if (File.Exists(ConfigPath)) File.Replace(temporaryPath, ConfigPath, BackupPath);
            else File.Move(temporaryPath, ConfigPath);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static AppConfig CreateDefaults()
    {
        return new AppConfig
        {
            Tunnels = new List<TunnelInfo>
            {
                new() { Port = 5432, Nickname = "PostgreSQL" },
                new() { Port = 6379, Nickname = "Redis" },
                new() { Port = 8080, Nickname = "Web App" },
                new() { Port = 3000, RemotePort = 3001, Nickname = "Dev Server" }
            }
        };
    }
}
