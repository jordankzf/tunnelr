using System.Windows;
using Tunnelr.Models;

namespace Tunnelr.Views;

public partial class ServerSettingsDialog : Window
{
    public string ServerAddress => txtServer.Text.Trim();
    public int SshPort { get; private set; }
    public string Username => txtUser.Text.Trim();

    public ServerSettingsDialog(AppConfig config, bool isFirstRun = false)
    {
        InitializeComponent();

        txtServer.Text = config.Server;
        txtPort.Text = config.Port.ToString();
        txtUser.Text = config.User;

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

        SshPort = port;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
