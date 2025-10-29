using System;
using System.Windows;
using System.Windows.Threading;
using Virgil.App.ViewModels;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };

   public MainWindow()
        {
            InitializeComponent();
            if (DataContext is DashboardViewModel vm)
            {
                _clock.Tick += (_, __) => vm.TickClock();
                _clock.Start();
                vm.Init();
            }
        }
    }
}
