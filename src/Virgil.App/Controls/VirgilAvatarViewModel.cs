using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Virgil.App.Controls
{
    public class VirgilAvatarViewModel : INotifyPropertyChanged
    {
        private Brush _color = Brushes.Blue;
        private string _message = string.Empty;

        public Brush Color
        {
            get => _color;
            set
            {
                _color = value;
                OnPropertyChanged();
            }
        }

        public string Message
        {
            get => _message;
            set
            {
                _message = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
