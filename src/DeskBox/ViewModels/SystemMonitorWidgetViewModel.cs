using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using DeskBox.Models;
using DeskBox.Services;
using Microsoft.UI.Dispatching;

namespace DeskBox.ViewModels;

/// <summary>
/// One graphics card row inside the system monitor widget.
/// </summary>
public sealed partial class SystemMonitorGpuRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _nameText = "—";

    [ObservableProperty]
    private string _utilText = "—";

    [ObservableProperty]
    private string _vramText = "—";

    [ObservableProperty]
    private string _tempText = "—";

    [ObservableProperty]
    private string _powerText = "—";

    [ObservableProperty]
    private double _utilValue;

    [ObservableProperty]
    private double _vramValue;
}

/// <summary>
/// One storage device row inside the system monitor widget.
/// </summary>
public sealed partial class SystemMonitorDriveRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _nameText = "—";

    [ObservableProperty]
    private string _readText = "—";

    [ObservableProperty]
    private string _writeText = "—";

    [ObservableProperty]
    private string _tempText = "—";
}

/// <summary>
/// View model for the system-monitor widget. It polls the local Strata
/// monitor dashboard on a loopback interval and exposes an already-formatted,
/// localized projection for the leaf view, plus launch/stop actions.
/// </summary>
public sealed partial class SystemMonitorWidgetViewModel : ObservableObject, IDisposable
{
    public const int StatusDashboardDown = 0;
    public const int StatusStopped = 1;
    public const int StatusRunning = 2;
    public const int StatusBusy = 3;

    private const int PollIntervalOnlineMs = 2000;
    private const int PollIntervalIdleMs = 5000;
    private const int PollIntervalDashboardDownMs = 10000;

    private readonly WidgetConfig _config;
    private readonly LocalizationService _localizationService;
    private readonly SystemMonitorService _service;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _pollTimer;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private bool _isDisposed;
    private bool _isWindowVisible = true;
    private bool _revealCompleted;
    private int _pollInFlight;
    private DateTime _statusMessageUntilUtc = DateTime.MinValue;

    [ObservableProperty]
    private string _modelLabel = "Model";

    [ObservableProperty]
    private string _modelText = "—";

    [ObservableProperty]
    private string _gpuLabel = "GPU";

    [ObservableProperty]
    private string _cpuLabel = "CPU";

    [ObservableProperty]
    private string _cpuUtilText = "—";

    [ObservableProperty]
    private double _cpuUtilValue;

    [ObservableProperty]
    private string _cpuTempText = "—";

    [ObservableProperty]
    private string _memoryLabel = "Memory";

    [ObservableProperty]
    private string _memoryText = "—";

    [ObservableProperty]
    private string _memoryTempText = "—";

    [ObservableProperty]
    private double _memoryValue;

    [ObservableProperty]
    private string _tokenLabel = "Tokens";

    [ObservableProperty]
    private string _tokenText = "—";

    [ObservableProperty]
    private string _throughputText = "—";

    [ObservableProperty]
    private string _processLabel = "Process";

    [ObservableProperty]
    private string _diskLabel = "Disks";

    [ObservableProperty]
    private string _diskUnitsText = "MB/s";

    [ObservableProperty]
    private string _processText = "—";

    [ObservableProperty]
    private string _powerLabel = "Power";

    [ObservableProperty]
    private string _powerText = "—";

    [ObservableProperty]
    private string _powerSubText = "—";

    [ObservableProperty]
    private string _costLabel = "Cost";

    [ObservableProperty]
    private string _costDayText = "—";

    [ObservableProperty]
    private string _costTotalText = "—";

    [ObservableProperty]
    private string _updatedText = "—";

    [ObservableProperty]
    private string _statusMessage = "—";

    [ObservableProperty]
    private string _launchButtonText = "Launch";

    [ObservableProperty]
    private string _stopButtonText = "Stop";

    [ObservableProperty]
    private int _statusLevel = StatusStopped;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _canLaunch = true;

    [ObservableProperty]
    private bool _canStop;

    public SystemMonitorWidgetViewModel(
        WidgetConfig config,
        LocalizationService localizationService,
        SystemMonitorService? service = null,
        DispatcherQueue? dispatcherQueue = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.WidgetKind != WidgetKind.SystemMonitor)
        {
            throw new ArgumentException("SystemMonitor content requires a SystemMonitor widget config.", nameof(config));
        }

        _config = config;
        _localizationService = localizationService;
        _service = service ?? new SystemMonitorService();
        _dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("SystemMonitor content must be created on a UI thread.");

        _pollTimer = _dispatcherQueue.CreateTimer();
        _pollTimer.IsRepeating = false;
        _pollTimer.Tick += PollTimer_Tick;

        RaiseLocalizedProperties();
        _localizationService.LanguageChanged += LocalizationService_LanguageChanged;
    }

    public WidgetConfig Config => _config;

    public ObservableCollection<SystemMonitorGpuRowViewModel> GpuRows { get; } = new();

    public ObservableCollection<SystemMonitorDriveRowViewModel> DriveRows { get; } = new();

    public Task InitializeAsync()
    {
        SetStatusMessage(_localizationService.T("SystemMonitor.Waiting"), sticky: false);
        return Task.CompletedTask;
    }

    public Task RefreshAsync()
    {
        return RunPollAsync(force: true);
    }

    public void ApplyAppearance()
    {
    }

    public void OnActivated()
    {
        if (_isWindowVisible)
        {
            SchedulePoll(TimeSpan.Zero);
        }
    }

    public void OnDeactivated()
    {
    }

    public void OnWindowVisibilityChanged(bool visible)
    {
        _isWindowVisible = visible;
        if (visible)
        {
            SchedulePoll(TimeSpan.Zero);
        }
        else
        {
            _pollTimer.Stop();
        }
    }

    public void OnWindowRevealCompleted()
    {
        _revealCompleted = true;
        SchedulePoll(TimeSpan.Zero);
    }

    public async Task RequestLaunchAsync()
    {
        await RunActionAsync(launch: true);
    }

    public async Task RequestStopAsync()
    {
        await RunActionAsync(launch: false);
    }

    private void LocalizationService_LanguageChanged()
    {
        RaiseLocalizedProperties();
        ReformatLastSnapshot();
    }

    private void RaiseLocalizedProperties()
    {
        ModelLabel = _localizationService.T("SystemMonitor.Label.Model");
        GpuLabel = _localizationService.T("SystemMonitor.Label.Gpu");
        CpuLabel = _localizationService.T("SystemMonitor.Label.Cpu");
        MemoryLabel = _localizationService.T("SystemMonitor.Label.Memory");
        TokenLabel = _localizationService.T("SystemMonitor.Label.Tokens");
        ProcessLabel = _localizationService.T("SystemMonitor.Label.Process");
        DiskLabel = _localizationService.T("SystemMonitor.Label.Disks");
        DiskUnitsText = _localizationService.T("SystemMonitor.Label.DiskUnits");
        PowerLabel = _localizationService.T("SystemMonitor.Label.Power");
        CostLabel = _localizationService.T("SystemMonitor.Label.Cost");
        LaunchButtonText = _localizationService.T("SystemMonitor.Launch");
        StopButtonText = _localizationService.T("SystemMonitor.Stop");
    }

    private void PollTimer_Tick(object? sender, object e)
    {
        _ = RunPollAsync(force: false);
    }

    private void SchedulePoll(TimeSpan interval)
    {
        if (_isDisposed || !_isWindowVisible || !_revealCompleted)
        {
            return;
        }

        _pollTimer.Stop();
        _pollTimer.Interval = interval;
        _pollTimer.Start();
    }

    private async Task RunPollAsync(bool force)
    {
        if (_isDisposed || Interlocked.Exchange(ref _pollInFlight, 1) == 1)
        {
            return;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
            SystemMonitorSnapshot? snapshot = await _service.TrySnapshotAsync(timeoutCts.Token);
            if (_isDisposed)
            {
                return;
            }

            if (snapshot is null)
            {
                StatusLevel = StatusDashboardDown;
                CanLaunch = false;
                CanStop = false;
                SetStatusMessage(_localizationService.T("SystemMonitor.DashboardOffline"), sticky: false);
                SchedulePoll(TimeSpan.FromMilliseconds(PollIntervalDashboardDownMs));
                return;
            }

            ApplySnapshot(snapshot);
            SchedulePoll(TimeSpan.FromMilliseconds(snapshot.StrataOnline ? PollIntervalOnlineMs : PollIntervalIdleMs));
        }
        catch (OperationCanceledException)
        {
            if (!_isDisposed)
            {
                SchedulePoll(TimeSpan.FromMilliseconds(PollIntervalDashboardDownMs));
            }
        }
        finally
        {
            Interlocked.Exchange(ref _pollInFlight, 0);
        }
    }

    private SystemMonitorSnapshot? _lastSnapshot;

    private void ApplySnapshot(SystemMonitorSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        ReformatLastSnapshot();
    }

    private void ReformatLastSnapshot()
    {
        SystemMonitorSnapshot? snapshot = _lastSnapshot;
        if (snapshot is null || _isDisposed)
        {
            return;
        }

        string model = snapshot.ModelId ?? "—";
        if (snapshot.ModelContext is int context && context > 0)
        {
            model += "  " + string.Format(CultureInfo.CurrentCulture, _localizationService.T("SystemMonitor.ContextSuffix"), context);
        }

        ModelText = model;

        SyncGpuRows(snapshot.Gpus);
        SyncDriveRows(snapshot.Drives);

        double? cpuUtil = snapshot.CpuUtilPercent;
        CpuUtilValue = cpuUtil is double utilValue ? ClampPercent(utilValue) : 0;
        CpuUtilText = cpuUtil is double util
            ? string.Format(CultureInfo.CurrentCulture, "{0:0}%", util)
            : "—";
        CpuTempText = FormatTemp(snapshot.CpuTempC);

        MemoryValue = ClampPercent(snapshot.MemPercent);
        MemoryText = string.Format(
            CultureInfo.CurrentCulture,
            "{0:0.0} / {1:0.0} GB  ({2:0}%)",
            snapshot.MemUsedGb,
            snapshot.MemTotalGb,
            snapshot.MemPercent);
        MemoryTempText = FormatTemp(snapshot.RamTempC);

        if (snapshot.TokDay is long day && snapshot.TokTotal is long total)
        {
            TokenText = string.Format(
                CultureInfo.CurrentCulture,
                "{0}  ·  {1}",
                string.Format(CultureInfo.CurrentCulture, _localizationService.T("SystemMonitor.TokToday"), day.ToString("N0", CultureInfo.CurrentCulture)),
                string.Format(CultureInfo.CurrentCulture, _localizationService.T("SystemMonitor.TokTotal"), total.ToString("N0", CultureInfo.CurrentCulture)));
        }
        else
        {
            TokenText = "—";
        }

        double? rate = snapshot.TokRatePerSecond ?? snapshot.AnswerTokPerSecond;
        ThroughputText = rate is double rateValue && rateValue > 0
            ? string.Format(CultureInfo.CurrentCulture, "{0:0.0} tok/s", rateValue)
            : _localizationService.T("SystemMonitor.Idle");

        ProcessText = snapshot.ProcPid is int pid
            ? string.Format(
                CultureInfo.CurrentCulture,
                "PID {0}  {1:0.0} GB",
                pid,
                snapshot.ProcMemMb / 1024.0)
            : _localizationService.T("SystemMonitor.NoProcess");

        PowerText = snapshot.TotalPowerW is double totalPower
            ? string.Format(CultureInfo.CurrentCulture, _localizationService.T("SystemMonitor.PowerFmt"), totalPower)
            : "—";
        if (snapshot.CpuPowerW is double cpuPower && snapshot.GpuPowerW is double gpuPower)
        {
            if (snapshot.CpuPowerMeasured)
            {
                PowerSubText = string.Format(
                    CultureInfo.CurrentCulture,
                    _localizationService.T("SystemMonitor.PowerSubFmtMeas"),
                    cpuPower,
                    gpuPower,
                    snapshot.PowerBaseW ?? 0d);
            }
            else
            {
                PowerSubText = string.Format(
                    CultureInfo.CurrentCulture,
                    _localizationService.T("SystemMonitor.PowerSubFmt"),
                    cpuPower,
                    gpuPower);
            }
        }
        else
        {
            PowerSubText = "—";
        }

        if (snapshot.EnergyDayKwh is double dayKwh && snapshot.EnergyDayCost is double dayCost)
        {
            CostDayText = string.Format(
                CultureInfo.CurrentCulture,
                _localizationService.T("SystemMonitor.CostDayFmt"),
                dayKwh.ToString("0.00", CultureInfo.CurrentCulture),
                dayCost.ToString("0.00", CultureInfo.CurrentCulture));
        }
        else
        {
            CostDayText = "—";
        }

        if (snapshot.EnergyTotalKwh is double totalKwh && snapshot.EnergyTotalCost is double totalCost)
        {
            CostTotalText = string.Format(
                CultureInfo.CurrentCulture,
                _localizationService.T("SystemMonitor.CostTotalFmt"),
                totalKwh.ToString("0.00", CultureInfo.CurrentCulture),
                totalCost.ToString("0.00", CultureInfo.CurrentCulture));
        }
        else
        {
            CostTotalText = "—";
        }

        UpdatedText = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);

        bool busyNow = IsBusy;
        StatusLevel = busyNow
            ? StatusBusy
            : snapshot.StrataOnline ? StatusRunning : StatusStopped;
        CanLaunch = !busyNow && !snapshot.StrataOnline;
        CanStop = !busyNow && snapshot.StrataOnline;

        if (ShouldShowStateMessage())
        {
            SetStatusMessage(
                _localizationService.T(snapshot.StrataOnline ? "SystemMonitor.Running" : "SystemMonitor.Stopped"),
                sticky: false);
        }
    }

    private void SyncGpuRows(IReadOnlyList<SystemMonitorGpuSnapshot> gpus)
    {
        while (GpuRows.Count > gpus.Count)
        {
            GpuRows.RemoveAt(GpuRows.Count - 1);
        }

        while (GpuRows.Count < gpus.Count)
        {
            GpuRows.Add(new SystemMonitorGpuRowViewModel());
        }

        for (int i = 0; i < gpus.Count; i++)
        {
            SystemMonitorGpuSnapshot gpu = gpus[i];
            SystemMonitorGpuRowViewModel row = GpuRows[i];
            row.NameText = "#" + i.ToString(CultureInfo.CurrentCulture) + "  " + CompactGpuName(gpu.Name);
            row.UtilValue = ClampPercent(gpu.UtilPercent);
            row.UtilText = string.Format(CultureInfo.CurrentCulture, "{0:0}%", gpu.UtilPercent);
            row.VramValue = gpu.VramTotalMib > 0
                ? ClampPercent(gpu.VramUsedMib / gpu.VramTotalMib * 100)
                : 0;
            row.VramText = string.Format(
                CultureInfo.CurrentCulture,
                "{0:0} / {1:0} MiB",
                gpu.VramUsedMib,
                gpu.VramTotalMib);
            row.TempText = FormatTemp(gpu.TempC);
            row.PowerText = gpu.PowerW is double power
                ? string.Format(CultureInfo.CurrentCulture, "{0:0.0} W", power)
                : "—";
        }
    }

    private void SyncDriveRows(IReadOnlyList<SystemMonitorDriveSnapshot> drives)
    {
        while (DriveRows.Count > drives.Count)
        {
            DriveRows.RemoveAt(DriveRows.Count - 1);
        }

        while (DriveRows.Count < drives.Count)
        {
            DriveRows.Add(new SystemMonitorDriveRowViewModel());
        }

        for (int i = 0; i < drives.Count; i++)
        {
            SystemMonitorDriveSnapshot drive = drives[i];
            SystemMonitorDriveRowViewModel row = DriveRows[i];
            row.NameText = drive.Name;
            row.ReadText = string.Format(CultureInfo.CurrentCulture, "R {0:0.0}", drive.ReadMBs);
            row.WriteText = string.Format(CultureInfo.CurrentCulture, "W {0:0.0}", drive.WriteMBs);
            row.TempText = FormatTemp(drive.TempC);
        }
    }

    private string FormatTemp(double? tempC)
    {
        return tempC is double temp
            ? string.Format(CultureInfo.CurrentCulture, "{0:0}\u00B0C", temp)
            : _localizationService.T("SystemMonitor.TempNA");
    }

    private bool ShouldShowStateMessage()
    {
        return !IsBusy && DateTime.UtcNow >= _statusMessageUntilUtc;
    }

    private void SetStatusMessage(string message, bool sticky)
    {
        StatusMessage = message;
        _statusMessageUntilUtc = sticky
            ? DateTime.UtcNow.AddSeconds(30)
            : DateTime.MinValue;
    }

    private static double ClampPercent(double value)
    {
        return value is < 0 or > 100 or double.NaN ? 0 : value;
    }

    private static string CompactGpuName(string name)
    {
        string compact = name
            .Replace("NVIDIA GeForce ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("AMD Radeon ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Intel(R) ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
        return string.IsNullOrEmpty(compact) ? "—" : compact;
    }

    private async Task RunActionAsync(bool launch)
    {
        if (_isDisposed || IsBusy)
        {
            return;
        }

        IsBusy = true;
        CanLaunch = false;
        CanStop = false;
        StatusLevel = StatusBusy;
        SetStatusMessage(_localizationService.T(launch ? "SystemMonitor.ActionStarting" : "SystemMonitor.ActionStopping"), sticky: true);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(6));
            (bool accepted, string? message) = launch
                ? await _service.LaunchAsync(timeoutCts.Token)
                : await _service.StopAsync(timeoutCts.Token);

            if (_isDisposed)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                SetStatusMessage(message, sticky: true);
            }
            else
            {
                SetStatusMessage(
                    _localizationService.T(accepted
                        ? launch ? "SystemMonitor.LaunchAccepted" : "SystemMonitor.StopAccepted"
                        : "SystemMonitor.ActionFailed"),
                    sticky: true);
            }

            SchedulePoll(TimeSpan.FromMilliseconds(accepted && launch ? PollIntervalIdleMs : 1500));
        }
        catch (OperationCanceledException)
        {
            if (!_isDisposed)
            {
                SetStatusMessage(_localizationService.T("SystemMonitor.ActionFailed"), sticky: true);
                SchedulePoll(TimeSpan.FromMilliseconds(PollIntervalDashboardDownMs));
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _localizationService.LanguageChanged -= LocalizationService_LanguageChanged;
        _pollTimer.Stop();
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
    }
}
