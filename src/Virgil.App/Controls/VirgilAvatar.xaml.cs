using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Virgil.App.Controls;

public partial class VirgilAvatar : UserControl
{
    public static readonly DependencyProperty FaceFillProperty = DependencyProperty.Register(
        nameof(FaceFill), typeof(Brush), typeof(VirgilAvatar), new PropertyMetadata(new SolidColorBrush(Color.FromRgb(91, 120, 78))));

    public Brush FaceFill
    {
        get => (Brush)GetValue(FaceFillProperty);
        set => SetValue(FaceFillProperty, value);
    }

    public VirgilAvatar()
    {
        InitializeComponent();
    }
}
