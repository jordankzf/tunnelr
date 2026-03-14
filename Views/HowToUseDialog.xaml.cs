using System.Windows;

namespace Tunnelr.Views;

public partial class HowToUseDialog : Window
{
    public HowToUseDialog()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
