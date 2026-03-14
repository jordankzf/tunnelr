using System.Windows;

namespace Tunnelr.Views;

public partial class DeleteTunnelDialog : Window
{
    public int SelectedIndex { get; private set; } = -1;
    private readonly bool _confirmDelete;

    public DeleteTunnelDialog(string[] items, bool confirmDelete = true)
    {
        _confirmDelete = confirmDelete;
        InitializeComponent();

        foreach (var item in items)
            lstTunnels.Items.Add(item);

        if (items.Length > 0)
            lstTunnels.SelectedIndex = 0;
    }

    /// <summary>
    /// Configure as a generic picker (no delete confirmation).
    /// </summary>
    public void SetPickerMode(string header, string buttonText)
    {
        txtHeader.Text = header;
        btnAction.Content = buttonText;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (lstTunnels.SelectedIndex < 0)
        {
            MessageBox.Show("Select a tunnel.", "No Selection",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_confirmDelete)
        {
            var result = MessageBox.Show(
                $"Delete {lstTunnels.SelectedItem}?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;
        }

        SelectedIndex = lstTunnels.SelectedIndex;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
