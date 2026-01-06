param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Update-File {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [scriptblock] $Transform
    )

    if (-not (Test-Path -Path $Path)) {
        throw "Missing file: $Path"
    }

    $original = Get-Content -Path $Path -Raw -ErrorAction Stop
    $updated = & $Transform $original

    if ($updated -ne $original) {
        Set-Content -Path $Path -Value $updated -Encoding UTF8
        Write-Host "Updated $Path"
    }
    else {
        Write-Host "No changes needed for $Path"
    }
}

$mainShellPath = Join-Path $PSScriptRoot 'src/Virgil.App/Views/MainShell.xaml'
Update-File -Path $mainShellPath -Transform {
    param($content)

    $avatarSnippet = @"
                <controls:AvatarControl Width=\"220\"
                                        Height=\"220\"
                                        DataContext=\"{Binding Monitoring}\"
                                        UseNewAvatar=\"True\" />
"@

    if ($content -notmatch "UseNewAvatar=\"True\"") {
        $content = $content -replace '(?s)\s*<controls:AvatarView[^>]*/>', "`n$avatarSnippet"
    }

    $debugPanel = @"
                <StackPanel x:Name=\"AvatarDebugPanel\"
                            Visibility=\"Collapsed\"
                            Margin=\"0,14,0,0\"
                            Width=\"240\"
                            DataContext=\"{Binding Monitoring}\">
                    <TextBlock Text=\"Avatar stress (debug)\" FontWeight=\"SemiBold\" Margin=\"0,0,0,4\" />
                    <CheckBox Content=\"Override telemetry\"
                              IsChecked=\"{Binding UseDebugStress, Mode=TwoWay}\"
                              Margin=\"0,0,0,6\" />
                    <Slider Minimum=\"0\"
                            Maximum=\"1\"
                            SmallChange=\"0.05\"
                            LargeChange=\"0.1\"
                            TickFrequency=\"0.05\"
                            TickPlacement=\"BottomRight\"
                            IsSnapToTickEnabled=\"True\"
                            IsEnabled=\"{Binding UseDebugStress}\"
                            Value=\"{Binding DebugStress, Mode=TwoWay}\"
                            ToolTip=\"Manual stress override for development\" />
                </StackPanel>
"@

    if ($content -notmatch "AvatarDebugPanel") {
        $content = $content -replace '(\s*<views:MetricsBar[^>]*/>)', "$1`n$debugPanel"
    }

    return $content
}

$mainShellCodeBehind = Join-Path $PSScriptRoot 'src/Virgil.App/Views/MainShell.xaml.cs'
Update-File -Path $mainShellCodeBehind -Transform {
    param($content)

    if ($content -notmatch 'AvatarDebugPanel.Visibility = Visibility.Visible;') {
        $content = $content -replace '(DataContext = mainVm;\s*)', "$1`n#if DEBUG`n            AvatarDebugPanel.Visibility = Visibility.Visible;`n#endif`n"
    }

    return $content
}

$monitoringVmPath = Join-Path $PSScriptRoot 'src/Virgil.App/ViewModels/MonitoringViewModel.cs'
Update-File -Path $monitoringVmPath -Transform {
    param($content)

    if ($content -notmatch 'UseDebugStress') {
        $debugProps = @"

#if DEBUG
        private bool _useDebugStress;
        public bool UseDebugStress
        {
            get => _useDebugStress;
            set
            {
                if (value == _useDebugStress) return;
                _useDebugStress = value;
                OnPropertyChanged();

                if (_useDebugStress)
                {
                    AvatarTelemetry.SetStress(_debugStress);
                }
            }
        }

        private double _debugStress = 0.25;
        public double DebugStress
        {
            get => _debugStress;
            set
            {
                var clamped = Math.Clamp(value, 0d, 1d);
                if (Math.Abs(_debugStress - clamped) < 0.0001) return;
                _debugStress = clamped;
                OnPropertyChanged();

                if (UseDebugStress)
                {
                    AvatarTelemetry.SetStress(_debugStress);
                }
            }
        }
#endif
"@

        $content = $content -replace '(public AvatarTelemetryAdapter AvatarTelemetry \{ get; \} = new\(\);)', "$1$debugProps"

        $snapshotBlock = @"
#if DEBUG
            if (UseDebugStress)
            {
                AvatarTelemetry.SetStress(_debugStress);
            }
            else
            {
                AvatarTelemetry.UpdateFromMetrics(
                    snapshot.CpuUsage,
                    snapshot.GpuUsage,
                    snapshot.RamUsage,
                    snapshot.CpuTemperature,
                    snapshot.GpuTemperature,
                    snapshot.DiskTemperature);
            }
#else
            AvatarTelemetry.UpdateFromMetrics(
                snapshot.CpuUsage,
                snapshot.GpuUsage,
                snapshot.RamUsage,
                snapshot.CpuTemperature,
                snapshot.GpuTemperature,
                snapshot.DiskTemperature);
#endif

"@

        $content = $content -replace '(AvatarTelemetry.UpdateFromMetrics\(\s*\n\s+snapshot\.CpuUsage,\s*\n\s+snapshot\.GpuUsage,\s*\n\s+snapshot\.RamUsage,\s*\n\s+snapshot\.CpuTemperature,\s*\n\s+snapshot\.GpuTemperature,\s*\n\s+snapshot\.DiskTemperature\);\s*)', $snapshotBlock

        $metricsBlock = @"
#if DEBUG
            if (UseDebugStress)
            {
                AvatarTelemetry.SetStress(_debugStress);
            }
            else
            {
                AvatarTelemetry.UpdateFromMetrics(
                    metrics.CpuUsage,
                    metrics.GpuUsage,
                    metrics.RamUsage,
                    metrics.CpuTemp,
                    metrics.GpuTemp,
                    metrics.DiskTemp);
            }
#else
            AvatarTelemetry.UpdateFromMetrics(
                metrics.CpuUsage,
                metrics.GpuUsage,
                metrics.RamUsage,
                metrics.CpuTemp,
                metrics.GpuTemp,
                metrics.DiskTemp);
#endif

"@

        $content = $content -replace '(AvatarTelemetry.UpdateFromMetrics\(\s*\n\s+metrics\.CpuUsage,\s*\n\s+metrics\.GpuUsage,\s*\n\s+metrics\.RamUsage,\s*\n\s+metrics\.CpuTemp,\s*\n\s+metrics\.GpuTemp,\s*\n\s+metrics\.DiskTemp\);\s*)', $metricsBlock
    }

    return $content
}

Write-Host 'Patch applied. You can rerun this script safely.'
