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

    public ServerSettingsDialog(AppConfig config, bool isFirstRun = false)
    {
        InitializeComponent();

        txtServer.Text = config.Server;
        txtPort.Text = config.Port.ToString();
        txtUser.Text = config.User;
        txtHealthInterval.Text = config.HealthCheckInterval.ToString();
        chkStartup.IsChecked = IsStartupEnabled();

        if (isFirstRun)
        {
            Title = "Welcome to Tunnelr - Configure Server";
            btnCancel.IsEnabled = false;
            btnCancel.Visibility = Visibility.Collapsed;
        }

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
            MessageBox.Show("Enter a valid health check interval (0 to disable).", "Invalid Interval",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SshPort = port;
        HealthCheckInterval = health;

        // Set or remove startup registry entry
        SetStartupEnabled(chkStartup.IsChecked == true);

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

    private static void SetStartupEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true);
            if (key == null) return;

            if (enabled)
            {
                var exePath = Environment.ProcessPath;
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
