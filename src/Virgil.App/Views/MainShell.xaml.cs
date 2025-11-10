using System;
using System.Windows;
using Virgil.App.Chat;
using Virgil.App.ViewModels;

namespace Virgil.App.Views
{
    public partial class MainShell : Window
    {
        private readonly ChatService _chat = new();
        private readonly ChatViewModel _vm;

        public MainShell()
        {
            InitializeComponent();
            _vm = new ChatViewModel(_chat);
            DataContext = _vm;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _chat.Post("Virgil est en ligne.");
            _chat.Post("Je garde un oeil sur la machine.", MessageType.Info, pinned: true);
            _chat.Post("Petit message éphémère qui va se désintégrer.", MessageType.Info, pinned: false, ttlMs: 3000);
        }
    }
}
