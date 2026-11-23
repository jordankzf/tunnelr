using System.Windows;
using Tunnelr.Services;
using Tunnelr.Views;
using System.Threading;
using System.Security.Principal;
using Microsoft.Win32;

namespace Tunnelr;

public partial class App : System.Windows.Application
{
    private FileStream? _configLock;
    private EventWaitHandle? _activation;
    private RegisteredWaitHandle? _listener;
    private static string ActivationName => @"Local\Tunnelr.Show." + WindowsIdentity.GetCurrent().User?.Value;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            _configLock = TunnelConfig.AcquireLock();
            _activation = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationName);
            Models.AppConfig config;
            bool firstRun;
            try
            {
                config = TunnelConfig.Load(out firstRun);
                if (!firstRun && !File.Exists(TunnelConfig.ConfigPath))
                {
                    if (!PreviewDialog.Confirm(null, config, Path.Combine(AppContext.BaseDirectory, "tunnels.json")))
                    { Shutdown(); return; }
                    TunnelConfig.Save(config);
                }
            }
            catch (InvalidDataException ex)
            {
                MessageBox.Show(ex.Message + "\nSelect a backup or an earlier configuration to recover.", "Configuration recovery");
                var picker = new OpenFileDialog { InitialDirectory = Path.GetDirectoryName(TunnelConfig.ConfigPath),
                    Filter = "Configuration files|*.json;*.previous;*.bak|All files|*.*" };
                if (picker.ShowDialog() != true) { Shutdown(); return; }
                config = TunnelConfig.Read(picker.FileName);
                if (!PreviewDialog.Confirm(null, config, picker.FileName)) { Shutdown(); return; }
                TunnelConfig.Save(config);
                firstRun = false;
            }
            var window = new MainWindow(config, firstRun);
            MainWindow = window;
            _listener = ThreadPool.RegisterWaitForSingleObject(_activation, (_, _) =>
                Dispatcher.BeginInvoke(new Action(window.Reveal)), null, -1, false);
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindow.Show();
        }
        catch (IOException ex) when (_configLock == null && (ex.HResult & 0xffff) == 32)
        {
            if (EventWaitHandle.TryOpenExisting(ActivationName, out var handle))
            { using (handle) handle.Set(); }
            else MessageBox.Show("Tunnelr is already running. Open it from the system tray.", "Tunnelr");
            Shutdown();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show(ex.Message, "Tunnelr configuration", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _listener?.Unregister(null);
        _activation?.Dispose();
        _configLock?.Dispose();
        base.OnExit(e);
    }
}
