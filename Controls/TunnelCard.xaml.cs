using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Tunnelr.Models;

namespace Tunnelr.Controls;

public partial class TunnelCard : UserControl
{
    public TunnelInfo? Tunnel { get; private set; }

    public event Action<TunnelCard>? ToggleRequested;

    private readonly Storyboard? _pulseStoryboard;

    // Frozen brushes for performance
    private static readonly SolidColorBrush CyanBrush = Freeze(new SolidColorBrush(Color.FromRgb(0, 229, 255)));
    private static readonly SolidColorBrush DimCyanBrush = Freeze(new SolidColorBrush(Color.FromRgb(0, 102, 122)));
    private static readonly SolidColorBrush MagentaBrush = Freeze(new SolidColorBrush(Color.FromRgb(255, 0, 170)));
    private static readonly SolidColorBrush ErrorRedBrush = Freeze(new SolidColorBrush(Color.FromRgb(255, 50, 50)));
    private static readonly SolidColorBrush DimRedBrush = Freeze(new SolidColorBrush(Color.FromRgb(140, 30, 30)));

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

        // Build pulse animation for active state
        var pulseAnim = new DoubleAnimation
        {
            From = 0.25,
            To = 0.6,
            Duration = TimeSpan.FromMilliseconds(1200),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        _pulseStoryboard = new Storyboard();
        Storyboard.SetTarget(pulseAnim, glowBorder);
        Storyboard.SetTargetProperty(pulseAnim, new PropertyPath("Effect.Opacity"));
        _pulseStoryboard.Children.Add(pulseAnim);

        // Hover effects
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

    public void UpdateVisualState()
    {
        if (Tunnel == null) return;

        if (Tunnel.HasError)
        {
            // Error: red glow
            cardBody.BorderBrush = ErrorRedBrush;
            cardGlow.Color = Color.FromRgb(255, 50, 50);
            cardGlow.Opacity = 0.4;
            orbFill.Color = Color.FromRgb(255, 50, 50);
            orbGlow.Fill = ErrorOrbGlow;
            txtPort.Foreground = ErrorRedBrush;
            try { _pulseStoryboard?.Stop(this); } catch { }
        }
        else if (Tunnel.IsActive)
        {
            // Active: cyan glow, bright orb
            cardBody.BorderBrush = CyanBrush;
            cardGlow.Color = Color.FromRgb(0, 229, 255);
            orbFill.Color = Color.FromRgb(0, 229, 255);
            orbGlow.Fill = CyanOrbGlow;
            txtPort.Foreground = CyanBrush;
            try { _pulseStoryboard?.Begin(this, true); } catch { }
        }
        else
        {
            // Inactive: dim, magenta port
            cardBody.BorderBrush = DimCyanBrush;
            cardGlow.Color = Color.FromRgb(0, 102, 122);
            cardGlow.Opacity = 0.15;
            orbFill.Color = Color.FromRgb(85, 51, 68);
            orbGlow.Fill = DimMagentaOrbGlow;
            txtPort.Foreground = MagentaBrush;
            try { _pulseStoryboard?.Stop(this); } catch { }
        }
    }

    private void SetHover(bool hover)
    {
        if (Tunnel == null) return;

        if (hover)
        {
            cardBody.BorderBrush = CyanBrush;
            cardGlow.BlurRadius = 20;
            if (!Tunnel.IsActive && !Tunnel.HasError) cardGlow.Opacity = 0.4;
        }
        else
        {
            UpdateVisualState();
            cardGlow.BlurRadius = 12;
        }
    }

    private void Card_Click(object sender, MouseButtonEventArgs e)
    {
        ToggleRequested?.Invoke(this);
    }
}
