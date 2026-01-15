using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace Virgil.App.Services
{
    public sealed class ActionProgressService : INotifyPropertyChanged
    {
        private static readonly Lazy<ActionProgressService> LazyInstance =
            new(() => new ActionProgressService());

        private readonly object _sync = new();
        private int _activeCount;
        private bool _isActive;
        private bool _isIndeterminate = true;
        private double _progressValue;

        private ActionProgressService()
        {
        }

        public static ActionProgressService Instance => LazyInstance.Value;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsActive
        {
            get => _isActive;
            private set => SetField(ref _isActive, value, nameof(IsActive));
        }

        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            private set => SetField(ref _isIndeterminate, value, nameof(IsIndeterminate));
        }

        public double ProgressValue
        {
            get => _progressValue;
            private set => SetField(ref _progressValue, value, nameof(ProgressValue));
        }

        public void StartIndeterminate()
        {
            lock (_sync)
            {
                _activeCount++;
                IsActive = true;
                IsIndeterminate = true;
                ProgressValue = 0;
            }
        }

        public void StartDeterminate()
        {
            lock (_sync)
            {
                _activeCount++;
                IsActive = true;
                IsIndeterminate = false;
                ProgressValue = 0;
            }
        }

        public void Report(double value)
        {
            lock (_sync)
            {
                if (_activeCount <= 0)
                {
                    return;
                }

                IsIndeterminate = false;
                ProgressValue = Math.Clamp(value, 0, 100);
            }
        }

        public void Complete()
        {
            lock (_sync)
            {
                if (_activeCount > 0)
                {
                    _activeCount--;
                }

                if (_activeCount == 0)
                {
                    IsActive = false;
                    IsIndeterminate = true;
                    ProgressValue = 0;
                }
            }
        }

        private void SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            NotifyPropertyChanged(propertyName);
        }

        private void NotifyPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler is null)
            {
                return;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => handler(this, new PropertyChangedEventArgs(propertyName)));
            }
            else
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
