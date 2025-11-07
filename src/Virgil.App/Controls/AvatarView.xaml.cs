using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Virgil.App.Controls;

public partial class AvatarView : UserControl
{
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(string), typeof(AvatarView),
            new PropertyMetadata(null, OnSourceChanged));

    public string? Source
    {
        get => (string?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    private readonly DispatcherTimer _blinkTimer = new();

    public AvatarView()
    {
        InitializeComponent();

        if (Resources["BreathStoryboard"] is Storyboard breath)
            breath.Begin(Host, true);

        _blinkTimer.Interval = TimeSpan.FromSeconds(RandomBlinkInterval());
        _blinkTimer.Tick += (_, _) =>
        {
            if (Resources["BlinkStoryboard"] is Storyboard blink)
                blink.Begin(Host, true);
            _blinkTimer.Interval = TimeSpan.FromSeconds(RandomBlinkInterval());
        };
        _blinkTimer.Start();
    }

    private static double RandomBlinkInterval()
    {
        var r = new Random();
        return 4.0 + r.NextDouble() * 4.0;
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (AvatarView)d;
        view.SetImageSafely(e.NewValue as string);
    }

    private void SetImageSafely(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = new Uri(path, UriKind.Absolute);
        bmp.EndInit();
        bmp.Freeze();

        // Charge la nouvelle image sur la couche front, puis fondu croisé
        ImgFront.Source = bmp;

        if (Resources["CrossfadeStoryboard"] is Storyboard fade)
        {
            fade.Completed += (_, _) =>
            {
                var tmp = ImgBack.Source;
                ImgBack.Source = ImgFront.Source;
                ImgFront.Source = tmp;

                ImgBack.Opacity = 1;
                ImgFront.Opacity = 0;
            };
            fade.Begin(Host, true);
        }
        else
        {
            ImgBack.Source = bmp;
            ImgFront.Source = null;
            ImgBack.Opacity = 1;
            ImgFront.Opacity = 0;
        }
    }
}
