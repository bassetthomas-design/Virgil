using System;

namespace Virgil.App.Controls;

public readonly struct ExpressionState
{
    public ExpressionState(double gazeX, double gazeY, double eyeOpenLeft, double eyeOpenRight, double squint, ExpressionMood mood)
    {
        GazeX = Math.Clamp(gazeX, -1d, 1d);
        GazeY = Math.Clamp(gazeY, -1d, 1d);
        EyeOpenLeft = Math.Clamp(eyeOpenLeft, 0d, 1d);
        EyeOpenRight = Math.Clamp(eyeOpenRight, 0d, 1d);
        Squint = Math.Clamp(squint, 0d, 1d);
        Mood = mood;
    }

    public double GazeX { get; }

    public double GazeY { get; }

    public double EyeOpenLeft { get; }

    public double EyeOpenRight { get; }

    public double Squint { get; }

    public ExpressionMood Mood { get; }

    public static ExpressionState Neutral => new(0d, 0d, 1d, 1d, 0d, ExpressionMood.Neutral);

    public ExpressionState With(
        double? gazeX = null,
        double? gazeY = null,
        double? eyeOpenLeft = null,
        double? eyeOpenRight = null,
        double? squint = null,
        ExpressionMood? mood = null)
        => new(
            gazeX ?? GazeX,
            gazeY ?? GazeY,
            eyeOpenLeft ?? EyeOpenLeft,
            eyeOpenRight ?? EyeOpenRight,
            squint ?? Squint,
            mood ?? Mood);
}

public enum ExpressionMood
{
    Neutral,
    Happy,
    Focused,
    Concerned,
    Annoyed
}
