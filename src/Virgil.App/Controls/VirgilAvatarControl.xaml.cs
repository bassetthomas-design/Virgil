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
    private readonly ExpressionEngine _expressionEngine = new();

    private SolidColorBrush AccentBrush => (SolidColorBrush)FindResource("AccentBrush");
    private SolidColorBrush AccentStrokeBrush => (SolidColorBrush)FindResource("AccentStrokeBrush");
    private SolidColorBrush FaceFillBrush => (SolidColorBrush)FindResource("FaceFillBrush");

    private double _smoothedStress;
    private double _targetStress;
    private double _timeToNextBlink;
    private double _blinkProgress;
    private bool _isBlinking;
    private ExpressionState _expressionState = ExpressionState.Neutral;

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

    public static readonly DependencyProperty IsWorkingProperty = DependencyProperty.Register(
        nameof(IsWorking),
        typeof(bool),
        typeof(VirgilAvatarControl),
        new PropertyMetadata(false, OnIsWorkingChanged));

    public bool IsWorking
    {
        get => (bool)GetValue(IsWorkingProperty);
        set => SetValue(IsWorkingProperty, value);
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

    private static void OnIsWorkingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VirgilAvatarControl control)
        {
            control._timeToNextBlink = control.ComputeNextBlinkTime();
            if (!control.IsAnimated)
            {
                control.UpdateVisuals(force: true);
            }
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
        _expressionState = _expressionEngine.Update(_animationTimer.Interval.TotalSeconds, _smoothedStress, IsWorking);
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
        var baseDelay = 2.9 - (_smoothedStress * 1.3) - (IsWorking ? 0.35 : 0d);
        return Math.Max(0.9, baseDelay) + (_random.NextDouble() * 0.9);
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

        var leftBaseOpen = 0.35 + (0.65 * _expressionState.EyeOpenLeft);
        var rightBaseOpen = 0.35 + (0.65 * _expressionState.EyeOpenRight);
        var squintFactor = 1 - (_expressionState.Squint * 0.35);
        var leftOpenness = Math.Max(0.18, leftBaseOpen * squintFactor * blinkScale);
        var rightOpenness = Math.Max(0.18, rightBaseOpen * squintFactor * blinkScale);
        var eyeScaleX = 1 + (_smoothedStress * 0.05) + (_expressionState.Squint * 0.08);

        LeftEyeScale.ScaleX = eyeScaleX;
        LeftEyeScale.ScaleY = leftOpenness;
        RightEyeScale.ScaleX = eyeScaleX;
        RightEyeScale.ScaleY = rightOpenness;

        var sizeScale = Math.Clamp(ActualWidth / 220d, 0.7, 1.4);
        var maxOffset = Math.Clamp(sizeScale * 3.5, 1d, 4d);
        var gazeOffset = new Point(_expressionState.GazeX * maxOffset, _expressionState.GazeY * maxOffset);

        LeftEyeOffset.X = -8 + gazeOffset.X;
        LeftEyeOffset.Y = gazeOffset.Y;
        RightEyeOffset.X = 8 + gazeOffset.X;
        RightEyeOffset.Y = gazeOffset.Y;

        EyesOffset.X = gazeOffset.X * 0.35;
        EyesOffset.Y = gazeOffset.Y * 0.35;
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
