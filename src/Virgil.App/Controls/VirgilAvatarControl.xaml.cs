using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Virgil.App.Controls;

public partial class VirgilAvatarControl : UserControl
{
    private readonly DispatcherTimer _animationTimer;
    private readonly Random _random = new();

    private SolidColorBrush AccentBrush => (SolidColorBrush)FindResource("AccentBrush");
    private SolidColorBrush AccentStrokeBrush => (SolidColorBrush)FindResource("AccentStrokeBrush");
    private SolidColorBrush FaceFillBrush => (SolidColorBrush)FindResource("FaceFillBrush");

    private double _smoothedStress;
    private double _targetStress;
    private double _timeToNextBlink;
    private double _blinkProgress;
    private bool _isBlinking;
    private Point _eyeOffset;
    private Point _eyeTargetOffset;
    private double _microMoveCooldown;

    public VirgilAvatarControl()
    {
        InitializeComponent();

        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };

        _animationTimer.Tick += OnAnimationTick;
        Loaded += (_, _) => StartOrStopTimer();
        Unloaded += (_, _) => _animationTimer.Stop();

        _timeToNextBlink = ComputeNextBlinkTime();
        UpdateVisuals(force: true);
    }

    public static readonly DependencyProperty StressProperty = DependencyProperty.Register(
        nameof(Stress),
        typeof(double),
        typeof(VirgilAvatarControl),
        new PropertyMetadata(0d, OnStressChanged));

    public double Stress
    {
        get => (double)GetValue(StressProperty);
        set => SetValue(StressProperty, Math.Clamp(value, 0d, 1d));
    }

    public static readonly DependencyProperty IsAnimatedProperty = DependencyProperty.Register(
        nameof(IsAnimated),
        typeof(bool),
        typeof(VirgilAvatarControl),
        new PropertyMetadata(true, OnIsAnimatedChanged));

    public bool IsAnimated
    {
        get => (bool)GetValue(IsAnimatedProperty);
        set => SetValue(IsAnimatedProperty, value);
    }

    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(VirgilAvatarState),
        typeof(VirgilAvatarControl),
        new PropertyMetadata(VirgilAvatarState.Idle, OnStateChanged));

    public VirgilAvatarState State
    {
        get => (VirgilAvatarState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    private static void OnStressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VirgilAvatarControl control)
        {
            control._targetStress = Math.Clamp((double)e.NewValue, 0d, 1d);
            if (!control.IsAnimated)
            {
                control._smoothedStress = control._targetStress;
                control.UpdateVisuals(force: true);
            }
        }
    }

    private static void OnIsAnimatedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VirgilAvatarControl control)
        {
            control.StartOrStopTimer();
        }
    }

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VirgilAvatarControl control)
        {
            control.UpdateGlowIntensity();
        }
    }

    private void StartOrStopTimer()
    {
        if (IsAnimated && IsLoaded)
        {
            _animationTimer.Start();
        }
        else
        {
            _animationTimer.Stop();
        }
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        const double smoothing = 0.12;
        _smoothedStress += (_targetStress - _smoothedStress) * smoothing;

        UpdateBlink(_animationTimer.Interval.TotalSeconds);
        UpdateMicroMovement(_animationTimer.Interval.TotalSeconds);
        UpdateGlowIntensity();
        UpdateVisuals();
    }

    private void UpdateBlink(double deltaSeconds)
    {
        if (!IsAnimated)
        {
            return;
        }

        if (_isBlinking)
        {
            _blinkProgress += deltaSeconds / 0.16;
            if (_blinkProgress >= 1)
            {
                _isBlinking = false;
                _timeToNextBlink = ComputeNextBlinkTime();
                _blinkProgress = 0;
            }
        }
        else
        {
            _timeToNextBlink -= deltaSeconds;
            if (_timeToNextBlink <= 0)
            {
                _isBlinking = true;
                _blinkProgress = 0;
            }
        }
    }

    private double ComputeNextBlinkTime()
    {
        var baseDelay = 2.8 - (_targetStress * 1.4);
        return Math.Max(1.1, baseDelay) + _random.NextDouble();
    }

    private void UpdateMicroMovement(double deltaSeconds)
    {
        _microMoveCooldown -= deltaSeconds;
        if (_microMoveCooldown <= 0)
        {
            var range = 1.5 + (_smoothedStress * 1.5);
            _eyeTargetOffset = new Point(
                (_random.NextDouble() * 2 - 1) * range,
                (_random.NextDouble() * 2 - 1) * range);
            _microMoveCooldown = 1.2 + _random.NextDouble() * 1.5;
        }

        var follow = 0.08;
        _eyeOffset = new Point(
            _eyeOffset.X + (_eyeTargetOffset.X - _eyeOffset.X) * follow,
            _eyeOffset.Y + (_eyeTargetOffset.Y - _eyeOffset.Y) * follow);
    }

    private void UpdateGlowIntensity()
    {
        var glowBoost = State switch
        {
            VirgilAvatarState.Critical => 0.4,
            VirgilAvatarState.Error => 0.35,
            VirgilAvatarState.Hot => 0.2,
            VirgilAvatarState.Working => 0.1,
            _ => 0
        };

        var intensity = Math.Clamp(0.6 + (_smoothedStress * 0.6) + glowBoost, 0.3, 1.2);
        RingGlow.Opacity = intensity;
        FaceGlow.Opacity = 0.6 + (_smoothedStress * 0.4);
    }

    private void UpdateVisuals(bool force = false)
    {
        var accent = ComputeAccentColor(_smoothedStress);
        if (force || AccentBrush.Color != accent)
        {
            AnimateColor(AccentBrush, accent, TimeSpan.FromMilliseconds(force ? 1 : 260));
            AnimateColor(AccentStrokeBrush, Color.FromArgb(128, accent.R, accent.G, accent.B), TimeSpan.FromMilliseconds(force ? 1 : 260));
            AnimateColor(FaceFillBrush, Color.FromArgb(255, (byte)(12 + accent.R / 6), (byte)(18 + accent.G / 6), (byte)(20 + accent.B / 6)), TimeSpan.FromMilliseconds(force ? 1 : 260));
            RingGlow.Color = Color.FromArgb(160, accent.R, accent.G, accent.B);
            FaceGlow.Color = Color.FromArgb(140, accent.R, accent.G, accent.B);
        }

        var blinkScale = _isBlinking
            ? 1 - 0.9 * Math.Sin(Math.Min(_blinkProgress, 1) * Math.PI)
            : 1.0;

        var tension = 1 + (_smoothedStress * 0.14);
        var openess = Math.Max(0.22, blinkScale * tension);

        LeftEyeScale.ScaleX = 1 + (_smoothedStress * 0.05);
        LeftEyeScale.ScaleY = openess;
        RightEyeScale.ScaleX = 1 + (_smoothedStress * 0.05);
        RightEyeScale.ScaleY = openess;

        LeftEyeOffset.X = -8 + _eyeOffset.X;
        LeftEyeOffset.Y = _eyeOffset.Y;
        RightEyeOffset.X = 8 + _eyeOffset.X;
        RightEyeOffset.Y = _eyeOffset.Y;

        EyesOffset.X = _eyeOffset.X * 0.35;
        EyesOffset.Y = _eyeOffset.Y * 0.35;
    }

    private static Color ComputeAccentColor(double stress)
    {
        stress = Math.Clamp(stress, 0d, 1d);
        var gradient = stress switch
        {
            <= 0.5 => Interpolate(Color.FromRgb(46, 204, 113), Color.FromRgb(255, 214, 10), stress / 0.5),
            <= 0.75 => Interpolate(Color.FromRgb(255, 214, 10), Color.FromRgb(255, 159, 67), (stress - 0.5) / 0.25),
            _ => Interpolate(Color.FromRgb(255, 159, 67), Color.FromRgb(235, 87, 87), (stress - 0.75) / 0.25)
        };

        return gradient;
    }

    private static Color Interpolate(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0d, 1d);
        byte Lerp(byte from, byte to) => (byte)(from + (to - from) * t);
        return Color.FromRgb(Lerp(a.R, b.R), Lerp(a.G, b.G), Lerp(a.B, b.B));
    }

    private static void AnimateColor(SolidColorBrush brush, Color target, TimeSpan duration)
    {
        var animation = new ColorAnimation
        {
            To = target,
            Duration = duration,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };

        brush.BeginAnimation(SolidColorBrush.ColorProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }
}

public enum VirgilAvatarState
{
    Idle,
    Working,
    Hot,
    Critical,
    Error,
    Gaming
}
