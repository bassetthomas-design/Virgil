using System.Windows;

namespace Virgil.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Nettoyage: ancien code référençait un bouton "ToggleSurveillance" inexistant.
        // On garde une fenêtre simple; le toggle sera recâblé via une PR dédiée.
    }
}
