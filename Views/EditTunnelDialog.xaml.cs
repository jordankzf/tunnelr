using System.Windows;

namespace Tunnelr.Views;

public partial class EditTunnelDialog : Window
{
    public int TunnelPort { get; private set; }
    public string Nickname { get; private set; } = string.Empty;

    public EditTunnelDialog(int currentPort, string currentNickname)
    {
        InitializeComponent();
        txtPort.Text = currentPort.ToString();
        txtNickname.Text = currentNickname;
        txtNickname.Focus();
        txtNickname.SelectAll();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(txtPort.Text, out var port) || port < 1 || port > 65535)
        {
            MessageBox.Show("Enter a valid port number (1-65535).", "Invalid Port",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(txtNickname.Text))
        {
            MessageBox.Show("Enter a nickname for this tunnel.", "Missing Nickname",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TunnelPort = port;
        Nickname = txtNickname.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
