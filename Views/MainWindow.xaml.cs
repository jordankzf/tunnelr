using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Tunnelr.Models;
using Tunnelr.Services;
using Forms = System.Windows.Forms;

namespace Tunnelr.Views;

public partial class MainWindow : Window
{
    private AppConfig _config;
    private readonly Forms.NotifyIcon _tray;
    private readonly Forms.ContextMenuStrip _trayMenu = new();
    private readonly HashSet<TunnelProcess> _observed = new();
    private bool _exiting;
    private string? _group;
    private ServerProfile Profile => _config.Current;
    private IEnumerable<TunnelInfo> AllTunnels => _config.AllProfiles.SelectMany(p => p.Tunnels);
    private IEnumerable<TunnelInfo> VisibleTunnels => Profile.Tunnels.Where(t =>
        _group == null || t.Group == _group);

    public MainWindow(AppConfig config, bool firstRun)
    {
        _config = config;
        InitializeComponent();
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Resources/app.ico")).Stream;
        _tray = new Forms.NotifyIcon { Icon = new System.Drawing.Icon(stream), Text = "Tunnelr 2",
            Visible = true, ContextMenuStrip = _trayMenu };
        _tray.DoubleClick += (_, _) => Reveal();
        _trayMenu.Opening += (_, _) => BuildTrayMenu();
        SystemEvents.PowerModeChanged += PowerChanged;
        Bind();
        if (firstRun) Loaded += (_, _) => EditProfile(Profile, false);
    }

    private void Bind()
    {
        profiles.Items.Clear();
        foreach (var profile in _config.AllProfiles)
            AddChoice(profiles, profile.Name, profile == Profile, () => SelectProfile(profile));
        var names = Profile.Tunnels.Select(t => t.Group).Where(g => !string.IsNullOrWhiteSpace(g)).Distinct().OrderBy(g => g).ToList();
        if (_group != null && !names.Contains(_group)) _group = null;
        groups.Items.Clear();
        AddChoice(groups, "All tunnels", _group == null, () => SelectGroup(null));
        foreach (var group in names) AddChoice(groups, group, group == _group, () => SelectGroup(group));
        groups.Visibility = names.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        profiles.Visibility = _config.Profiles.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        foreach (var session in _observed) session.Changed -= UpdateStatus;
        _observed.Clear();
        foreach (var tunnel in AllTunnels)
            if (_observed.Add(tunnel.Session)) tunnel.Session.Changed += UpdateStatus;
        RenderCards();
    }

    private void RenderCards()
    {
        var visible = VisibleTunnels.ToList();
        cards.ItemsSource = visible;
        empty.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        txtServer.Text = $"{Profile.User}@{Profile.Server}:{Profile.Port}";
        txtStatus.Text = $"{Profile.Tunnels.Count(t => t.Session.State == "Listening")} of {Profile.Tunnels.Count} listening";
        txtStatus.ToolTip = _group == null ? Profile.Name : $"{Profile.Name} · {_group}";
        _tray.Text = $"Tunnelr · {AllTunnels.Count(t => t.Session.State == "Listening")} listening";
        bulk.IsEnabled = VisibleTunnels.Any();
        bulk.Content = VisibleTunnels.Any() && VisibleTunnels.All(t => t.Session.Wanted) ? "All Off" : "All On";
    }

    private bool Persist(Action change, Action undo)
    {
        change();
        try { TunnelConfig.Save(_config); return true; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            undo();
            MessageBox.Show(this, ex.Message, "Settings could not be saved");
            return false;
        }
    }

    private static void AddChoice(MenuItem menu, string label, bool selected, Action choose)
    {
        var item = new MenuItem { Header = label, IsChecked = selected };
        item.Click += (_, _) => choose();
        menu.Items.Add(item);
    }

    private void SelectProfile(ServerProfile chosen)
    {
        if (chosen == Profile) return;
        var old = _config.SelectedProfile;
        if (Persist(() => _config.SelectedProfile = chosen.Name, () => _config.SelectedProfile = old)) _group = null;
        Bind();
    }

    private void SelectGroup(string? group) { _group = group; Bind(); }

    private void PickEdit_Click(object sender, RoutedEventArgs e) => PickTunnel((Button)sender, EditTunnel);
    private void PickDelete_Click(object sender, RoutedEventArgs e) => PickTunnel((Button)sender, DeleteTunnel);

    private void PickTunnel(Button anchor, Action<TunnelInfo> select)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var tunnel in VisibleTunnels)
        {
            var item = new MenuItem { Header = $"{tunnel.Port} · {tunnel.Nickname}" };
            item.Click += (_, _) => select(tunnel);
            menu.Items.Add(item);
        }
        if (menu.Items.Count > 0) menu.IsOpen = true;
    }

    private void Help_Click(object sender, RoutedEventArgs e) => PreviewDialog.ShowText(this, "Using Tunnelr",
        "Click a card to open or close its tunnel. Cyan means listening. Hover for the destination and connection details.\n\nRight-click a card to copy its endpoint, edit it, or read SSH output. Use File for profiles, imports, and backups. All On and All Off apply to the visible group.");

    private void EditProfile(ServerProfile profile, bool adding)
    {
        var dialog = new ServerSettingsDialog(profile) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var prior = (profile.Name, profile.Server, profile.Port, profile.User, profile.Reconnect, profile.HealthCheckInterval);
        var selected = _config.SelectedProfile;
        if (!Persist(() =>
        {
            (profile.Name, profile.Server, profile.Port, profile.User, profile.Reconnect, profile.HealthCheckInterval) =
                (dialog.ProfileName, dialog.ServerAddress, dialog.SshPort, dialog.Username, dialog.Reconnect, dialog.HealthCheckInterval);
            if (adding) _config.Profiles.Add(profile);
            _config.SelectedProfile = profile.Name;
        }, () =>
        {
            (profile.Name, profile.Server, profile.Port, profile.User, profile.Reconnect, profile.HealthCheckInterval) = prior;
            if (adding) _config.Profiles.Remove(profile);
            _config.SelectedProfile = selected;
        })) return;
        if ((profile.Server, profile.Port, profile.User) != (prior.Server, prior.Port, prior.User))
            foreach (var tunnel in profile.Tunnels) tunnel.Session.Stop();
        if (!profile.Reconnect)
            foreach (var tunnel in profile.Tunnels.Where(t => t.Session.State is "Retrying" or "Paused")) tunnel.Session.Stop();
        ServerSettingsDialog.SetStartupEnabled(dialog.Startup);
        Bind();
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => EditProfile(Profile, false);
    private void NewProfile_Click(object sender, RoutedEventArgs e) => EditProfile(new ServerProfile { Name = "" }, true);
    private void Add_Click(object sender, RoutedEventArgs e) => EditTunnel(null);

    private void EditTunnel(TunnelInfo? old)
    {
        var dialog = new EditTunnelDialog(old) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var next = new TunnelInfo { Port = dialog.TunnelPort, RemotePort = dialog.RemotePort,
            Group = dialog.Group, Nickname = dialog.Nickname };
        var list = Profile.Tunnels;
        var index = old == null ? list.Count : list.IndexOf(old);
        if (!Persist(() => { if (old == null) list.Add(next); else list[index] = next; },
                     () => { if (old == null) list.Remove(next); else list[index] = old; })) return;
        var restart = old?.Session.Wanted == true;
        old?.Session.Stop();
        Bind();
        if (restart) next.Session.Start(next, Profile);
    }

    private void DeleteTunnel(TunnelInfo tunnel)
    {
        if (MessageBox.Show(this, $"Delete {tunnel.Nickname} from {Profile.Name}?", "Delete tunnel",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        int index = Profile.Tunnels.IndexOf(tunnel);
        if (!Persist(() => Profile.Tunnels.RemoveAt(index), () => Profile.Tunnels.Insert(index, tunnel))) return;
        tunnel.Session.Stop();
        Bind();
    }

    private void Toggle(TunnelInfo tunnel, ServerProfile profile)
    {
        if (profile.Server == "your.server.com") { EditProfile(profile, false); return; }
        if (tunnel.Session.Wanted) tunnel.Session.Stop();
        else if (tunnel.Session.State == "Verify host")
        {
            if (MessageBox.Show(this, $"Open OpenSSH to verify the fingerprint for {profile.Server}? Compare it with your server's fingerprint before accepting. Then click Connect.", "Verify SSH host",
                MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            try { TunnelProcess.VerifyHost(profile); tunnel.Session.Stop(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "OpenSSH"); }
        }
        else tunnel.Session.Start(tunnel, profile);
    }

    private void Toggle_Click(object sender, RoutedEventArgs e) => Toggle((TunnelInfo)((Button)sender).DataContext, Profile);
    private void Bulk_Click(object sender, RoutedEventArgs e)
    {
        if (Profile.Server == "your.server.com") { EditProfile(Profile, false); return; }
        var tunnels = VisibleTunnels.ToList();
        var stop = tunnels.All(t => t.Session.Wanted);
        foreach (var t in tunnels)
            if (stop) t.Session.Stop();
            else if (!t.Session.Wanted && t.Session.State != "Verify host") t.Session.Start(t, Profile);
    }

    private void CardMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var button = (Button)sender;
        var tunnel = (TunnelInfo)button.DataContext;
        var menu = button.ContextMenu;
        menu.Items.Clear();
        void Add(string label, Action action)
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }
        Add("Copy endpoint", () => Clipboard.SetText($"127.0.0.1:{tunnel.Port}"));
        Add("Edit tunnel…", () => EditTunnel(tunnel));
        Add("SSH output…", () => PreviewDialog.ShowText(this, tunnel.Nickname,
            tunnel.Session.Output.Length == 0 ? "No SSH output yet." : tunnel.Session.Output));
        Add("Delete tunnel…", () => DeleteTunnel(tunnel));
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Filter = "Tunnelr settings|*.json;*.previous;*.bak|All files|*.*" };
        if (picker.ShowDialog(this) == true) Import(picker.FileName);
    }
    private void Restore_Click(object sender, RoutedEventArgs e) => Import(TunnelConfig.BackupPath);
    private void Import(string path)
    {
        try
        {
            var next = TunnelConfig.Read(path);
            if (!PreviewDialog.Confirm(this, next, path)) return;
            TunnelConfig.Save(next);
            foreach (var tunnel in AllTunnels) tunnel.Session.Stop();
            _config = next;
            Bind();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        { MessageBox.Show(this, ex.Message, "Configuration recovery"); }
    }

    private void Folder_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(Path.GetDirectoryName(TunnelConfig.ConfigPath)!) { UseShellExecute = true });

    private void PowerChanged(object sender, PowerModeChangedEventArgs e) => Dispatcher.InvokeAsync(() =>
    {
        if (_exiting) return;
        foreach (var profile in _config.AllProfiles)
            foreach (var tunnel in profile.Tunnels)
            {
                if (e.Mode == PowerModes.Suspend && tunnel.Session.Wanted) tunnel.Session.Stop(profile.Reconnect);
                if (e.Mode == PowerModes.Resume && tunnel.Session.State == "Paused") tunnel.Session.Start(tunnel, profile);
            }
    });

    private void BuildTrayMenu()
    {
        while (_trayMenu.Items.Count > 0) _trayMenu.Items[0].Dispose();
        _trayMenu.Items.Add("Open Tunnelr", null, (_, _) => Reveal());
        foreach (var profile in _config.AllProfiles)
        {
            var submenu = new Forms.ToolStripMenuItem(profile.Name);
            foreach (var tunnel in profile.Tunnels)
                submenu.DropDownItems.Add($"{tunnel.Nickname} · {tunnel.Port} · {tunnel.Session.State}",
                    null, (_, _) => Toggle(tunnel, profile));
            _trayMenu.Items.Add(submenu);
        }
        _trayMenu.Items.Add("Exit Tunnelr", null, (_, _) => Exit());
    }

    public void Reveal() { Show(); WindowState = WindowState.Normal; Activate(); }
    private void Exit_Click(object sender, RoutedEventArgs e) => Exit();
    private void Exit() { _exiting = true; Close(); }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_exiting) { e.Cancel = true; Hide(); }
        else
        {
            SystemEvents.PowerModeChanged -= PowerChanged;
            foreach (var tunnel in AllTunnels) tunnel.Session.Stop();
            _tray.Visible = false;
            _tray.Dispose();
            _trayMenu.Dispose();
        }
        base.OnClosing(e);
    }
}
