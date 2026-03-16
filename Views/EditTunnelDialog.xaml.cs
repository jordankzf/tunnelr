using System.Windows;

namespace Tunnelr.Views;

public partial class EditTunnelDialog : Window
{
    public int TunnelPort { get; private set; }
    public int RemotePort { get; private set; }
    public string Nickname { get; private set; } = string.Empty;

    public EditTunnelDialog(int currentPort, int currentRemotePort, string currentNickname)
    {
        InitializeComponent();
        txtPort.Text = currentPort.ToString();
        txtRemotePort.Text = currentRemotePort > 0 ? currentRemotePort.ToString() : "";
        txtNickname.Text = currentNickname;
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
