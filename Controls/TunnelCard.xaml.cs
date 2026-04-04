using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Tunnelr.Models;

namespace Tunnelr.Controls;

public partial class TunnelCard : UserControl
{
    public TunnelInfo? Tunnel { get; private set; }

    public event Action<TunnelCard>? ToggleRequested;

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
    }

    public void Bind(TunnelInfo tunnel)
    {
        Tunnel = tunnel;
        txtNickname.Text = tunnel.Nickname;
        txtPort.Text = tunnel.Port.ToString();
        UpdateVisualState();
    }

    public void Cleanup()
    {
        StopPulse();
        _pulseStoryboard = null;
    }

    public void UpdateVisualState()
    {
        if (Tunnel == null) return;

        StopPulse();

        if (Tunnel.HasError)
        {
            cardBody.BorderBrush = ErrorRedBrush;
            glowColor.Color = Color.FromRgb(255, 50, 50);
            glowBorder.Opacity = 0.4;
            orbFill.Color = Color.FromRgb(255, 50, 50);
            orbGlow.Fill = ErrorOrbGlow;
            txtPort.Foreground = ErrorRedBrush;
        }
        else if (Tunnel.IsActive)
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
            if (!Tunnel.IsActive && !Tunnel.HasError) glowBorder.Opacity = 0.4;
        }
        else
        {
            UpdateVisualState();
        }
    }

    private void Card_Click(object sender, MouseButtonEventArgs e)
    {
        ToggleRequested?.Invoke(this);
    }
}
