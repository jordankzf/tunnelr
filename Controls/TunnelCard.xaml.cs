using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Tunnelr.Models;

namespace Tunnelr.Controls;

public partial class TunnelCard : Button
{
    private TunnelInfo? Tunnel => DataContext as TunnelInfo;
    private TunnelInfo? _observed;

    private Storyboard? _pulseStoryboard;

    private static readonly SolidColorBrush CyanBrush = Freeze(new SolidColorBrush(Color.FromRgb(0, 229, 255)));
    private static readonly SolidColorBrush DimCyanBrush = Freeze(new SolidColorBrush(Color.FromRgb(0, 102, 122)));
    private static readonly SolidColorBrush MagentaBrush = Freeze(new SolidColorBrush(Color.FromRgb(255, 0, 170)));
    private static readonly SolidColorBrush ErrorRedBrush = Freeze(new SolidColorBrush(Color.FromRgb(255, 50, 50)));

    private static readonly RadialGradientBrush CyanOrbGlow = Freeze(new RadialGradientBrush(
        Color.FromArgb(100, 0, 229, 255), Color.FromArgb(0, 0, 229, 255)));
    private static readonly RadialGradientBrush DimMagentaOrbGlow = Freeze(new RadialGradientBrush(
        Color.FromArgb(30, 255, 0, 170), Color.FromArgb(0, 255, 0, 170)));
    private static readonly RadialGradientBrush ErrorOrbGlow = Freeze(new RadialGradientBrush(
        Color.FromArgb(100, 255, 50, 50), Color.FromArgb(0, 255, 50, 50)));

    private static T Freeze<T>(T brush) where T : Freezable
    {
        brush.Freeze();
        return brush;
    }

    public TunnelCard()
    {
        InitializeComponent();

        MouseEnter += (s, e) => SetHover(true);
        MouseLeave += (s, e) => SetHover(false);
        Loaded += (_, _) => Observe(Tunnel);
        Unloaded += (_, _) => Observe(null);
        DataContextChanged += (_, _) => { if (IsLoaded) Observe(Tunnel); };
    }

    private void Observe(TunnelInfo? tunnel)
    {
        if (_observed != null) _observed.Session.Changed -= UpdateVisualState;
        _observed = tunnel;
        if (tunnel != null)
        {
            tunnel.Session.Changed += UpdateVisualState;
            UpdateVisualState();
        }
        else StopPulse();
    }

    public void UpdateVisualState()
    {
        if (Tunnel == null) return;

        StopPulse();

        var state = Tunnel.Session.State;
        stateLabel.Visibility = state is "Off" or "Listening" ? Visibility.Collapsed : Visibility.Visible;
        if (state is "Failed" or "Blocked" or "Verify host")
        {
            cardBody.BorderBrush = ErrorRedBrush;
            glowColor.Color = Color.FromRgb(255, 50, 50);
            glowBorder.Opacity = 0.4;
            orbFill.Color = Color.FromRgb(255, 50, 50);
            orbGlow.Fill = ErrorOrbGlow;
            txtPort.Foreground = ErrorRedBrush;
        }
        else if (Tunnel.Session.State == "Listening")
        {
            cardBody.BorderBrush = CyanBrush;
            glowColor.Color = Color.FromRgb(0, 229, 255);
            glowBorder.Opacity = 0.25;
            orbFill.Color = Color.FromRgb(0, 229, 255);
            orbGlow.Fill = CyanOrbGlow;
            txtPort.Foreground = CyanBrush;
            StartPulse();
        }
        else
        {
            cardBody.BorderBrush = DimCyanBrush;
            glowColor.Color = Color.FromRgb(0, 102, 122);
            glowBorder.Opacity = 0.15;
            orbFill.Color = Color.FromRgb(85, 51, 68);
            orbGlow.Fill = DimMagentaOrbGlow;
            txtPort.Foreground = MagentaBrush;
        }
    }

    private void StartPulse()
    {
        var anim = new DoubleAnimation
        {
            From = 0.25,
            To = 0.6,
            Duration = TimeSpan.FromMilliseconds(1200),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        _pulseStoryboard = new Storyboard();
        Storyboard.SetTarget(anim, glowBorder);
        Storyboard.SetTargetProperty(anim, new PropertyPath(OpacityProperty));
        _pulseStoryboard.Children.Add(anim);
        _pulseStoryboard.Begin(this, true);
    }

    private void StopPulse()
    {
        if (_pulseStoryboard != null)
        {
            try { _pulseStoryboard.Stop(this); } catch { }
            _pulseStoryboard = null;
        }
    }

    private void SetHover(bool hover)
    {
        if (Tunnel == null) return;

        if (hover)
        {
            cardBody.BorderBrush = CyanBrush;
            if (Tunnel.Session.State == "Off") glowBorder.Opacity = 0.4;
        }
        else
        {
            UpdateVisualState();
        }
    }

}
