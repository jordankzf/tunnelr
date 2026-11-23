using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tunnelr.Models;

namespace Tunnelr.Views;

internal sealed class PreviewDialog : Window
{
    public static bool Confirm(Window? owner, AppConfig config, string path)
    {
        var profiles = config.AllProfiles.Select(p => $"{p.Name}    {p.User}@{p.Server}:{p.Port}\n" +
            string.Join("\n", p.Tunnels.Select(t => $"  {t.Nickname}    {t.Port} → localhost:{t.EffectiveRemotePort}    {t.Group}")));
        var text = $"{path}\n\n{string.Join("\n\n", profiles)}\n\nImporting replaces all profiles and stops current tunnels. Your previous settings are backed up.";
        return new PreviewDialog(owner, "Review configuration", text, "Import").ShowDialog() == true;
    }

    public static void ShowText(Window owner, string title, string text) =>
        new PreviewDialog(owner, title, text, null).ShowDialog();

    private PreviewDialog(Window? owner, string title, string text, string? accept)
    {
        Owner = owner;
        Title = title;
        Background = (Brush)FindResource("BgDarkBrush");
        Width = 660; Height = 430;
        WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        var panel = new DockPanel { Margin = new Thickness(14) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(buttons, Dock.Bottom);
        var close = new Button { Content = accept == null ? "Close" : "Cancel", IsCancel = true, Padding = new Thickness(15,5,15,5) };
        close.Style = (Style)FindResource("NeonButton");
        buttons.Children.Add(close);
        if (accept != null)
        {
            var apply = new Button { Content = accept, Margin = new Thickness(8,0,0,0), Padding = close.Padding };
            apply.Style = close.Style;
            apply.Click += (_, _) => DialogResult = true;
            buttons.Children.Add(apply);
        }
        panel.Children.Add(buttons);
        panel.Children.Add(new TextBox { Style = (Style)FindResource("CyberTextBox"), Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0,0,0,10) });
        Content = panel;
    }
}
