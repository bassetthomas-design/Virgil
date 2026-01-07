using System;
using System.Windows;
using Virgil.App.Utils;

namespace Virgil.App.Controls;

public sealed class ExpressionEngine
{
    private readonly Random _random;
    private ExpressionState _current = ExpressionState.Neutral;
    private Point _gazeTarget;
    private double _timeToNextGazeShift;
    private double _timeToNextAsymmetry;
    private double _asymmetry;
    private double _winkCooldown;
    private double _winkDuration;
    private double _winkProgress;
    private bool _isWinking;
    private bool _winkLeft;

    public ExpressionEngine(Random? random = null)
    {
        _random = random ?? new Random();
        _timeToNextGazeShift = RandomRange(2.5, 5.5);
        _timeToNextAsymmetry = RandomRange(3, 6);
        _winkCooldown = RandomRange(120, 300);
    }

    public ExpressionState Update(double deltaSeconds, double stress, bool isWorking)
    {
        stress = ValueSanitizer.Sanitize01(stress, 0d);

        UpdateGaze(deltaSeconds, stress, isWorking);
        UpdateAsymmetry(deltaSeconds, isWorking);
        UpdateWink(deltaSeconds);

        var mood = ResolveMood(stress, isWorking);
        var squint = ComputeSquint(stress, isWorking, mood);
        var baseOpen = Math.Clamp(1d - (stress * 0.32) - (isWorking ? 0.06 : 0d), 0.55, 1d);

        var leftOpen = Math.Clamp(baseOpen + _asymmetry, 0.3, 1d);
        var rightOpen = Math.Clamp(baseOpen - _asymmetry, 0.3, 1d);

        if (_isWinking && _winkDuration > 0)
        {
            var winkPhase = Math.Min(_winkProgress / _winkDuration, 1d);
            var winkScale = 1d - (0.85 * Math.Sin(winkPhase * Math.PI));
            if (_winkLeft)
            {
                leftOpen *= winkScale;
            }
            else
            {
                rightOpen *= winkScale;
            }
        }

        var target = new ExpressionState(
            _gazeTarget.X,
            _gazeTarget.Y,
            leftOpen,
            rightOpen,
            squint,
            mood);

        var smoothingTime = 0.2 + (stress * 0.35) + (isWorking ? 0.05 : 0d);
        var t = 1d - Math.Exp(-deltaSeconds / smoothingTime);

        _current = new ExpressionState(
            Lerp(_current.GazeX, target.GazeX, t),
            Lerp(_current.GazeY, target.GazeY, t),
            Lerp(_current.EyeOpenLeft, target.EyeOpenLeft, t),
            Lerp(_current.EyeOpenRight, target.EyeOpenRight, t),
            Lerp(_current.Squint, target.Squint, t),
            target.Mood);

        return _current;
    }

    private void UpdateGaze(double deltaSeconds, double stress, bool isWorking)
    {
        _timeToNextGazeShift -= deltaSeconds;
        if (_timeToNextGazeShift > 0)
        {
            return;
        }

        var baseRange = isWorking ? 0.55 : 0.45;
        baseRange -= stress * 0.3;
        baseRange = Math.Clamp(baseRange, 0.18, 0.6);

        var yRange = baseRange * 0.55;
        _gazeTarget = new Point(RandomRange(-baseRange, baseRange), RandomRange(-yRange, yRange));

        _timeToNextGazeShift = isWorking
            ? RandomRange(2, 6)
            : (stress >= 0.75 ? RandomRange(6, 12) : RandomRange(4, 9));
    }

    private void UpdateAsymmetry(double deltaSeconds, bool isWorking)
    {
        if (!isWorking)
        {
            _asymmetry = 0;
            _timeToNextAsymmetry = RandomRange(3, 6);
            return;
        }

        _timeToNextAsymmetry -= deltaSeconds;
        if (_timeToNextAsymmetry > 0)
        {
            return;
        }

        _asymmetry = RandomRange(-0.08, 0.08);
        _timeToNextAsymmetry = RandomRange(2.5, 5.5);
    }

    private void UpdateWink(double deltaSeconds)
    {
        if (_isWinking)
        {
            _winkProgress += deltaSeconds;
            if (_winkProgress >= _winkDuration)
            {
                _isWinking = false;
                _winkProgress = 0;
            }

            return;
        }

        _winkCooldown -= deltaSeconds;
        if (_winkCooldown > 0)
        {
            return;
        }

        _isWinking = true;
        _winkDuration = RandomRange(0.18, 0.28);
        _winkProgress = 0;
        _winkLeft = _random.NextDouble() < 0.5;
        _winkCooldown = RandomRange(120, 300);
    }

    private static ExpressionMood ResolveMood(double stress, bool isWorking)
    {
        if (stress <= 0.35)
        {
            return ExpressionMood.Happy;
        }

        if (stress <= 0.6)
        {
            return isWorking ? ExpressionMood.Focused : ExpressionMood.Neutral;
        }

        if (stress <= 0.8)
        {
            return ExpressionMood.Concerned;
        }

        return ExpressionMood.Annoyed;
    }

    private static double ComputeSquint(double stress, bool isWorking, ExpressionMood mood)
    {
        var moodBoost = mood switch
        {
            ExpressionMood.Happy => -0.05,
            ExpressionMood.Focused => 0.1,
            ExpressionMood.Concerned => 0.2,
            ExpressionMood.Annoyed => 0.32,
            _ => 0
        };

        var squint = 0.08 + (stress * 0.55) + (isWorking ? 0.12 : 0d) + moodBoost;
        return Math.Clamp(squint, 0d, 1d);
    }

    private double RandomRange(double min, double max)
        => min + (_random.NextDouble() * (max - min));

    private static double Lerp(double from, double to, double t)
        => from + ((to - from) * Math.Clamp(t, 0d, 1d));
}
