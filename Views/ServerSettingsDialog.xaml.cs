using System.Windows;
using Microsoft.Win32;
using Tunnelr.Models;

namespace Tunnelr.Views;

public partial class ServerSettingsDialog : Window
{
    private const string RegistryRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "Tunnelr";

    public string ServerAddress => txtServer.Text.Trim();
    public int SshPort { get; private set; }
    public string Username => txtUser.Text.Trim();
    public int HealthCheckInterval { get; private set; }

    public string ProfileName => profileName.Text.Trim();
    public bool Reconnect => reconnect.IsChecked == true;
    public bool Startup => chkStartup.IsChecked == true;

    public ServerSettingsDialog(ServerProfile config)
    {
        InitializeComponent();

        profileName.Text = config.Name;
        reconnect.IsChecked = config.Reconnect;
        txtServer.Text = config.Server == "your.server.com" ? "" : config.Server;
        txtPort.Text = config.Port.ToString();
        txtUser.Text = config.User;
        txtHealthInterval.Text = config.HealthCheckInterval.ToString();
        chkStartup.IsChecked = IsStartupEnabled();

        txtServer.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtServer.Text))
        {
            MessageBox.Show("Enter a server address.", "Missing Server",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(txtPort.Text, out var port) || port < 1 || port > 65535)
        {
            MessageBox.Show("Enter a valid SSH port (1-65535).", "Invalid Port",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(txtUser.Text))
        {
            MessageBox.Show("Enter a username.", "Missing Username",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(txtHealthInterval.Text, out var health) || health < 0)
        {
            MessageBox.Show("Enter a retry interval in seconds.", "Invalid Interval",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SshPort = port;
        HealthCheckInterval = health;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, false);
            return key?.GetValue(AppName) != null;
        }
        catch { return false; }
    }

    public static void SetStartupEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true);
            if (key == null) return;

            if (enabled)
            {
                var installedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "Tunnelr", "tunnelr.exe");
                var exePath = File.Exists(installedPath) ? installedPath : Environment.ProcessPath;
                if (exePath != null)
                    key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch { }
    }
}
