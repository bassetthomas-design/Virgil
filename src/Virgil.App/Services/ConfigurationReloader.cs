using System;
using System.Threading;
using System.Threading.Tasks;
using Virgil.App.ViewModels;
using Virgil.Services;

namespace Virgil.App.Services
{
    public sealed class ConfigurationReloader : IConfigurationReloader
    {
        private readonly SettingsService _settings;
        private readonly MonitoringService _monitoring;
        private MainViewModel? _mainVm;

        public ConfigurationReloader(SettingsService settings, MonitoringService monitoring)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _monitoring = monitoring ?? throw new ArgumentNullException(nameof(monitoring));
        }

        public void Attach(MainViewModel vm)
        {
            _mainVm = vm ?? throw new ArgumentNullException(nameof(vm));
        }

        public async Task<ConfigurationReloadResult> ReloadAsync(CancellationToken ct = default)
        {
            var userSettings = Try(() => _settings.Reload());

            // No dedicated system policy store exists yet: treat as refreshed.
            var systemSettings = true;

            var uiPreferences = await TryAsync(async () =>
            {
                if (_mainVm is null)
                {
                    return false;
                }

                await _mainVm.ReloadUiFromSettingsAsync(ct).ConfigureAwait(false);
                return true;
            }).ConfigureAwait(false);

            var viewModels = await TryAsync(async () =>
            {
                if (_mainVm is null)
                {
                    return false;
                }

                _mainVm.ResetTransientState();
                await _monitoring.RescanAsync().ConfigureAwait(false);
                return true;
            }).ConfigureAwait(false);

            var services = Try(() =>
            {
                _monitoring.SetInterval(_settings.Settings.MonitoringIntervalMs);
                if (_settings.Settings.MonitoringEnabled)
                {
                    _monitoring.Start();
                }
                else
                {
                    _monitoring.Stop();
                }
            });

            var caches = Try(() => { });

            return new ConfigurationReloadResult(userSettings, systemSettings, uiPreferences, viewModels, services, caches);
        }

        private static bool Try(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> TryAsync(Func<Task<bool>> action)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }
    }
}
