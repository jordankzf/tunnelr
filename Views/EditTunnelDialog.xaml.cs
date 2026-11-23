using System.Windows;

namespace Tunnelr.Views;

public partial class EditTunnelDialog : Window
{
    public int TunnelPort { get; private set; }
    public int RemotePort { get; private set; }
    public string Nickname { get; private set; } = string.Empty;

    public string Group => groupName.Text.Trim();

    public EditTunnelDialog(Tunnelr.Models.TunnelInfo? tunnel)
    {
        InitializeComponent();
        Title = heading.Text = tunnel == null ? "Add tunnel" : "Edit tunnel";
        groupName.Text = tunnel?.Group ?? "";
        txtPort.Text = tunnel?.Port.ToString() ?? "";
        txtRemotePort.Text = tunnel?.RemotePort > 0 ? tunnel.RemotePort.ToString() : "";
        txtNickname.Text = tunnel?.Nickname ?? "";
        txtNickname.Focus();
        txtNickname.SelectAll();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(txtPort.Text, out var port) || port < 1 || port > 65535)
        {
            MessageBox.Show("Enter a valid local port number (1-65535).", "Invalid Port",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int remotePort = 0;
        if (!string.IsNullOrWhiteSpace(txtRemotePort.Text))
        {
            if (!int.TryParse(txtRemotePort.Text, out remotePort) || remotePort < 1 || remotePort > 65535)
            {
                MessageBox.Show("Enter a valid remote port number (1-65535), or leave blank to match local port.", "Invalid Port",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(txtNickname.Text))
        {
            MessageBox.Show("Enter a nickname for this tunnel.", "Missing Nickname",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TunnelPort = port;
        RemotePort = remotePort;
        Nickname = txtNickname.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
