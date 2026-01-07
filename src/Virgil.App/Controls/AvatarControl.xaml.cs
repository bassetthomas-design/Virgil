using System;
using System.Windows;
using System.Windows.Controls;

namespace Virgil.App.Controls;

public partial class AvatarControl : UserControl
{
    public AvatarControl()
    {
        InitializeComponent();
        UpdateAvatarVisibility();
    }

    public static readonly DependencyProperty UseNewAvatarProperty = DependencyProperty.Register(
        nameof(UseNewAvatar),
        typeof(bool),
        typeof(AvatarControl),
        new PropertyMetadata(GetDefaultFlag(), OnUseNewAvatarChanged));

    public bool UseNewAvatar
    {
        get => (bool)GetValue(UseNewAvatarProperty);
        set => SetValue(UseNewAvatarProperty, value);
    }

    public static readonly DependencyProperty IsWorkingProperty = DependencyProperty.Register(
        nameof(IsWorking),
        typeof(bool),
        typeof(AvatarControl),
        new PropertyMetadata(false));

    public bool IsWorking
    {
        get => (bool)GetValue(IsWorkingProperty);
        set => SetValue(IsWorkingProperty, value);
    }

    private static void OnUseNewAvatarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AvatarControl control)
        {
            control.UpdateAvatarVisibility();
        }
    }

    private static bool GetDefaultFlag()
    {
        var env = Environment.GetEnvironmentVariable("VIRGIL_USE_NEW_AVATAR");
        if (string.IsNullOrWhiteSpace(env))
        {
            return true;
        }

        return bool.TryParse(env, out var parsed) && parsed;
    }

    private void UpdateAvatarVisibility()
    {
        if (VectorAvatar is null || LegacyAvatar is null)
        {
            return;
        }

        VectorAvatar.Visibility = UseNewAvatar ? Visibility.Visible : Visibility.Collapsed;
        LegacyAvatar.Visibility = UseNewAvatar ? Visibility.Collapsed : Visibility.Visible;
    }
}
