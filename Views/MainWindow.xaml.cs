using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Tunnelr.Controls;
using Tunnelr.Models;
using Tunnelr.Services;
using WinForms = System.Windows.Forms;

namespace Tunnelr.Views;

public partial class MainWindow : Window
{
    private AppConfig _config;
    private WinForms.NotifyIcon _trayIcon = null!;
    private WinForms.ContextMenuStrip _trayMenu = null!;
    private DispatcherTimer? _healthTimer;
    private bool _reallyClosing;

    public MainWindow()
    {
        _config = TunnelConfig.Load();
        InitializeComponent();

        // First-run: force server configuration if no config file existed
        if (!TunnelConfig.ConfigExists())
        {
            var dlg = new ServerSettingsDialog(_config, isFirstRun: true);
            if (dlg.ShowDialog() == true)
            {
                _config.Server = dlg.ServerAddress;
                _config.Port = dlg.SshPort;
                _config.User = dlg.Username;
                _config.HealthCheckInterval = dlg.HealthCheckInterval;
                TunnelConfig.Save(_config);
            }
        }

        SetupTray();
        SetupHealthTimer();
        RebuildCards();
        UpdateStatus();
    }

    // ─── Card management ───

    private void RebuildCards()
    {
        cardPanel.Children.Clear();
        foreach (var tunnel in _config.Tunnels)
        {
            var card = new TunnelCard();
            card.Bind(tunnel);
            card.ToggleRequested += OnCardToggle;
            cardPanel.Children.Add(card);
        }
    }

    private void RefreshAllCards()
    {
        foreach (TunnelCard card in cardPanel.Children)
            card.UpdateVisualState();
    }

    private void UpdateStatus()
    {
        var active = _config.Tunnels.Count(t => t.IsActive);
        var total = _config.Tunnels.Count;

        txtStatus.Text = $"{active} of {total} tunnels active";
        txtServer.Text = $"{_config.User}@{_config.Server}:{_config.Port}";
        btnToggleAll.Content = (active == total && total > 0) ? "All Off" : "All On";

        UpdateTrayTooltip();
    }

    // ─── Tunnel toggling ───

    private void OnCardToggle(TunnelCard card)
    {
        if (card.Tunnel == null) return;
        ToggleTunnel(card.Tunnel);
        card.UpdateVisualState();
        UpdateStatus();
    }

    private void ToggleTunnel(TunnelInfo tunnel)
    {
        if (tunnel.IsActive)
        {
            TunnelProcess.Stop(tunnel);
            ShowBalloon("Tunnel Disconnected", $"Port {tunnel.Port} ({tunnel.Nickname})");
        }
        else
        {
            if (!TunnelProcess.Start(tunnel, _config))
            {
                var errorMsg = tunnel.ErrorMessage ?? "Unknown error";
                MessageBox.Show(
                    $"Failed to start SSH tunnel on port {tunnel.Port}.\n\n{errorMsg}\n\n" +
                    "Make sure ssh.exe is available and your key is loaded.",
                    "Tunnel Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            ShowBalloon("Tunnel Connected", $"Port {tunnel.Port} ({tunnel.Nickname})");
        }
    }

    // ─── Button handlers ───

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AddTunnelDialog { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            if (_config.Tunnels.Any(t => t.Port == dlg.TunnelPort))
            {
                MessageBox.Show($"Port {dlg.TunnelPort} is already configured.",
                    "Duplicate Port", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _config.Tunnels.Add(new TunnelInfo
            {
                Port = dlg.TunnelPort,
                RemotePort = dlg.RemotePort,
                Nickname = dlg.Nickname
            });
            TunnelConfig.Save(_config);
            RebuildCards();
            UpdateStatus();
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_config.Tunnels.Count == 0) return;

        // Pick which tunnel to edit
        var items = _config.Tunnels.Select(t => $"{t.Port} - {t.Nickname}").ToArray();
        var pickDlg = new DeleteTunnelDialog(items, confirmDelete: false)
        {
            Owner = this,
            Title = "Select Tunnel to Edit"
        };
        pickDlg.SetPickerMode("// SELECT TUNNEL TO EDIT", "Select");
        if (pickDlg.ShowDialog() != true || pickDlg.SelectedIndex < 0) return;

        var tunnel = _config.Tunnels[pickDlg.SelectedIndex];
        var wasActive = tunnel.IsActive;

        var editDlg = new EditTunnelDialog(tunnel.Port, tunnel.RemotePort, tunnel.Nickname) { Owner = this };
        if (editDlg.ShowDialog() == true)
        {
            // Check for duplicate port (if port changed)
            if (editDlg.TunnelPort != tunnel.Port &&
                _config.Tunnels.Any(t => t.Port == editDlg.TunnelPort))
            {
                MessageBox.Show($"Port {editDlg.TunnelPort} is already configured.",
                    "Duplicate Port", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // If active and port changed, restart the tunnel
            if (wasActive && (editDlg.TunnelPort != tunnel.Port || editDlg.RemotePort != tunnel.RemotePort))
            {
                TunnelProcess.Stop(tunnel);
                tunnel.Port = editDlg.TunnelPort;
                tunnel.RemotePort = editDlg.RemotePort;
                tunnel.Nickname = editDlg.Nickname;
                TunnelProcess.Start(tunnel, _config);
            }
            else
            {
                tunnel.Port = editDlg.TunnelPort;
                tunnel.RemotePort = editDlg.RemotePort;
                tunnel.Nickname = editDlg.Nickname;
            }

            TunnelConfig.Save(_config);
            RebuildCards();
            UpdateStatus();
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_config.Tunnels.Count == 0) return;

        var items = _config.Tunnels.Select(t => $"{t.Port} - {t.Nickname}").ToArray();
        var dlg = new DeleteTunnelDialog(items) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.SelectedIndex >= 0 && dlg.SelectedIndex < _config.Tunnels.Count)
        {
            var tunnel = _config.Tunnels[dlg.SelectedIndex];
            TunnelProcess.Stop(tunnel);
            _config.Tunnels.RemoveAt(dlg.SelectedIndex);
            TunnelConfig.Save(_config);
            RebuildCards();
            UpdateStatus();
        }
    }

    private void ToggleAll_Click(object sender, RoutedEventArgs e)
    {
        var allActive = _config.Tunnels.All(t => t.IsActive) && _config.Tunnels.Count > 0;

        foreach (var tunnel in _config.Tunnels)
        {
            if (allActive)
            {
                if (tunnel.IsActive) TunnelProcess.Stop(tunnel);
            }
            else
            {
                if (!tunnel.IsActive) TunnelProcess.Start(tunnel, _config);
            }
        }

        RefreshAllCards();
        UpdateStatus();
        ShowBalloon(
            allActive ? "All Tunnels Disconnected" : "All Tunnels Connected",
            $"{_config.Tunnels.Count} tunnels");
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RunHealthCheck();
    }

    // ─── Menu handlers ───

    private void ServerSettings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ServerSettingsDialog(_config) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            // Stop all active tunnels before changing server
            var hadActive = _config.Tunnels.Any(t => t.IsActive);
            if (hadActive)
            {
                TunnelProcess.StopAll(_config.Tunnels);
            }

            _config.Server = dlg.ServerAddress;
            _config.Port = dlg.SshPort;
            _config.User = dlg.Username;
            _config.HealthCheckInterval = dlg.HealthCheckInterval;
            TunnelConfig.Save(_config);

            // Reconfigure health timer with new interval
            SetupHealthTimer();

            RefreshAllCards();
            UpdateStatus();

            if (hadActive)
            {
                MessageBox.Show(
                    "Server settings changed. All active tunnels have been stopped.\n" +
                    "Reconnect them with the new settings.",
                    "Settings Updated", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void HowToUse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new HowToUseDialog { Owner = this };
        dlg.ShowDialog();
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => ExitApplication();

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Tunnelr v1.0\nSSH Tunnel Switchboard\n\n" +
            $"Server: {_config.User}@{_config.Server}:{_config.Port}\n\n" +
            "Made with <3 by @jordankzf",
            "About Tunnelr", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ─── System tray ───

    private void SetupTray()
    {
        _trayMenu = new WinForms.ContextMenuStrip();
        _trayMenu.Opening += TrayMenu_Opening;

        var iconStream = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/Resources/app.ico"))?.Stream;
        var trayIcon = iconStream != null
            ? new System.Drawing.Icon(iconStream)
            : System.Drawing.SystemIcons.Application;

        _trayIcon = new WinForms.NotifyIcon
        {
            Text = "Tunnelr",
            Icon = trayIcon,
            Visible = true,
            ContextMenuStrip = _trayMenu
        };
        _trayIcon.DoubleClick += (s, e) => ShowFromTray();
    }

    private void TrayMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _trayMenu.Items.Clear();

        var showItem = new WinForms.ToolStripMenuItem("Show Tunnelr");
        showItem.Font = new System.Drawing.Font(showItem.Font, System.Drawing.FontStyle.Bold);
        showItem.Click += (s, ev) => Dispatcher.Invoke(ShowFromTray);
        _trayMenu.Items.Add(showItem);
        _trayMenu.Items.Add(new WinForms.ToolStripSeparator());

        foreach (var tunnel in _config.Tunnels)
        {
            var t = tunnel;
            var item = new WinForms.ToolStripMenuItem($"{t.Port} - {t.Nickname}")
            {
                Checked = t.IsActive
            };
            item.Click += (s, ev) => Dispatcher.Invoke(() =>
            {
                ToggleTunnel(t);
                RefreshAllCards();
                UpdateStatus();
            });
            _trayMenu.Items.Add(item);
        }

        _trayMenu.Items.Add(new WinForms.ToolStripSeparator());
        var exitItem = new WinForms.ToolStripMenuItem("Exit");
        exitItem.Click += (s, ev) => Dispatcher.Invoke(ExitApplication);
        _trayMenu.Items.Add(exitItem);
    }

    private void UpdateTrayTooltip()
    {
        var active = _config.Tunnels.Count(t => t.IsActive);
        _trayIcon.Text = $"Tunnelr - {active}/{_config.Tunnels.Count} active";
    }

    private void ShowBalloon(string title, string text)
    {
        _trayIcon.BalloonTipTitle = title;
        _trayIcon.BalloonTipText = text;
        _trayIcon.BalloonTipIcon = WinForms.ToolTipIcon.Info;
        _trayIcon.ShowBalloonTip(2000);
    }

    private void ShowFromTray()
    {
        this.Show();
        this.WindowState = WindowState.Normal;
        this.Activate();
    }

    // ─── Health check timer ───

    private void SetupHealthTimer()
    {
        // Stop existing timer if any
        _healthTimer?.Stop();
        _healthTimer = null;

        if (_config.HealthCheckInterval <= 0) return;

        _healthTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_config.HealthCheckInterval)
        };
        _healthTimer.Tick += (s, e) => RunHealthCheck();
        _healthTimer.Start();
    }

    private void RunHealthCheck()
    {
        bool changed = false;
        foreach (var tunnel in _config.Tunnels)
        {
            if (tunnel.IsActive && tunnel.SshProcess != null && tunnel.SshProcess.HasExited)
            {
                var exitCode = tunnel.SshProcess.ExitCode;
                var stderr = TunnelProcess.GetError(tunnel);

                tunnel.IsActive = false;
                tunnel.HasError = true;
                tunnel.ErrorMessage = stderr ?? $"SSH process exited with code {exitCode}";
                tunnel.SshProcess.Dispose();
                tunnel.SshProcess = null;

                ShowBalloon("Tunnel Failed",
                    $"Port {tunnel.Port} ({tunnel.Nickname}) disconnected unexpectedly.");
                changed = true;
            }
        }
        if (changed)
        {
            RefreshAllCards();
            UpdateStatus();
        }
    }

    // ─── Window lifecycle ───

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_reallyClosing)
        {
            e.Cancel = true;
            this.Hide();
            ShowBalloon("Tunnelr", "Still running in the system tray.");
        }
        else
        {
            _healthTimer?.Stop();
            TunnelProcess.StopAll(_config.Tunnels);
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.OnClosing(e);
    }

    private void ExitApplication()
    {
        _reallyClosing = true;
        Close();
    }
}
