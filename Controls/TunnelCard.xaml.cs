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

        if (Tunnel.IsActive)
        {
            // Active: cyan glow, bright orb
            cardBody.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 229, 255));
            cardGlow.Color = Color.FromRgb(0, 229, 255);
            orbFill.Color = Color.FromRgb(0, 229, 255);
            orbGlow.Fill = new RadialGradientBrush(
                Color.FromArgb(100, 0, 229, 255), Color.FromArgb(0, 0, 229, 255));
            txtPort.Foreground = new SolidColorBrush(Color.FromRgb(0, 229, 255));
            try { _pulseStoryboard?.Begin(this, true); } catch { }
        }
        else
        {
            // Inactive: dim, magenta port
            cardBody.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 102, 122));
            cardGlow.Color = Color.FromRgb(0, 102, 122);
            cardGlow.Opacity = 0.15;
            orbFill.Color = Color.FromRgb(85, 51, 68);
            orbGlow.Fill = new RadialGradientBrush(
                Color.FromArgb(30, 255, 0, 170), Color.FromArgb(0, 255, 0, 170));
            txtPort.Foreground = new SolidColorBrush(Color.FromRgb(255, 0, 170));
            try { _pulseStoryboard?.Stop(this); } catch { }
        }
    }

    private void SetHover(bool hover)
    {
        if (Tunnel == null) return;

        if (hover)
        {
            cardBody.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 229, 255));
            cardGlow.BlurRadius = 20;
            if (!Tunnel.IsActive) cardGlow.Opacity = 0.4;
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
