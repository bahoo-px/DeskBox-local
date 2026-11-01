using DeskBox.Models;
using DeskBox.Services;

namespace DeskBox.ViewModels;

/// <summary>
/// Drag-drop permission diagnostics, batch 49 form: the diagnose/repair
/// computation and the localized text projection run on the shell (host
/// services), and the results are pushed into the backup settings editor
/// (<see cref="Features.Backup.BackupSettingsViewModel"/>) which owns the
/// section's XAML binding surface. The editor holds no diagnostic state of
/// its own beyond the pushed texts.
/// </summary>
public partial class SettingsViewModel
{
    public void RefreshDragDropPermissionDiagnostic()
    {
        try
        {
            _dragDropPermissionDiagnostic = DragDropPermissionService.Diagnose(_localizationService);
            App.Log(
                "[DragDropPermission] " +
                $"issue={_dragDropPermissionDiagnostic.Issue} severity={_dragDropPermissionDiagnostic.Severity} " +
                $"process='{_dragDropPermissionDiagnostic.CurrentProcessIntegrity}' " +
                $"explorer='{_dragDropPermissionDiagnostic.ExplorerIntegrity}' " +
                $"uac='{_dragDropPermissionDiagnostic.UacStatus}' " +
                $"appCompat='{_dragDropPermissionDiagnostic.AppCompatStatus}' " +
                $"startup='{_dragDropPermissionDiagnostic.StartupStatus}'");
        }
        catch (Exception ex)
        {
            App.Log($"[DragDropPermission] Diagnose failed: {ex}");
            _dragDropPermissionDiagnostic = new DragDropPermissionDiagnostic(
                DragDropDiagnosticSeverity.Error,
                DragDropDiagnosticIssue.None,
                _localizationService.T("Settings.DragDropPermission.DiagnoseFailedSummary"),
                ex.Message,
                _localizationService.T("Settings.DragDropPermission.Unknown"),
                _localizationService.T("Settings.DragDropPermission.Unknown"),
                _localizationService.T("Settings.DragDropPermission.Unknown"),
                _localizationService.T("Settings.DragDropPermission.Unknown"),
                _localizationService.T("Settings.DragDropPermission.Unknown"),
                _localizationService.T("Settings.DragDropPermission.Unknown"),
                false,
                false,
                false,
                false,
                false,
                false,
                false);
        }

        PushDragDropDiagnosticProjection();
    }

    public DragDropPermissionRepairResult RepairDragDropPermission()
    {
        _backupSettings.SetDragDropRepairBusy(true);
        try
        {
            var result = DragDropPermissionService.Repair(_settingsService);
            _backupSettings.SetDragDropRepairStatus(result.Success
                ? _localizationService.Format("Settings.DragDropPermission.RepairStatus", result.RepairedCount)
                : _localizationService.Format("Settings.DragDropPermission.RepairFailedStatus", result.FailureMessage));
            RefreshDragDropPermissionDiagnostic();
            return result;
        }
        finally
        {
            _backupSettings.SetDragDropRepairBusy(false);
        }
    }

    /// <summary>
    /// Re-projects the localized diagnostic texts from the last diagnose
    /// run without re-running it (the language-changed path).
    /// </summary>
    private void PushDragDropDiagnosticProjection()
    {
        DragDropPermissionDiagnostic? diagnostic = _dragDropPermissionDiagnostic;
        _backupSettings.SetDragDropDiagnostic(new(
            GetDragDropPermissionSummaryText(),
            GetDragDropPermissionDetailText(),
            diagnostic?.CurrentProcessIntegrity ?? _localizationService.T("Settings.DragDropPermission.Unknown"),
            diagnostic?.ExplorerIntegrity ?? _localizationService.T("Settings.DragDropPermission.Unknown"),
            diagnostic?.UacStatus ?? _localizationService.T("Settings.DragDropPermission.Unknown"),
            diagnostic?.AppCompatStatus ?? _localizationService.T("Settings.DragDropPermission.Unknown"),
            diagnostic?.StartupStatus ?? _localizationService.T("Settings.DragDropPermission.Unknown"),
            diagnostic?.ShortcutStatus ?? _localizationService.T("Settings.DragDropPermission.Unknown"),
            CanRepairFromDiagnostic));
    }

    private bool CanRepairFromDiagnostic =>
        _dragDropPermissionDiagnostic is not null &&
        (_dragDropPermissionDiagnostic.HasAppCompatIssue ||
         _dragDropPermissionDiagnostic.HasStartupIssue ||
         _dragDropPermissionDiagnostic.HasShortcutIssue ||
         _dragDropPermissionDiagnostic.NeedsRelaunch);

    private string GetDragDropPermissionSummaryText()
    {
        if (_dragDropPermissionDiagnostic is null)
        {
            return _localizationService.T("Settings.DragDropPermission.NotChecked");
        }

        return _dragDropPermissionDiagnostic.Issue switch
        {
            DragDropDiagnosticIssue.UacDisabled => _localizationService.T("Settings.DragDropPermission.Summary.UacDisabled"),
            DragDropDiagnosticIssue.PermissionMismatch => _localizationService.T("Settings.DragDropPermission.Summary.PermissionMismatch"),
            DragDropDiagnosticIssue.AppCompatIssue => _localizationService.T("Settings.DragDropPermission.Summary.AppCompatIssue"),
            DragDropDiagnosticIssue.StartupShortcutIssue => _localizationService.T("Settings.DragDropPermission.Summary.StartupShortcutIssue"),
            _ => _localizationService.T("Settings.DragDropPermission.Summary.Ok")
        };
    }

    private string GetDragDropPermissionDetailText()
    {
        if (_dragDropPermissionDiagnostic is null)
        {
            return _localizationService.T("Settings.DragDropPermission.NotCheckedDetail");
        }

        return _dragDropPermissionDiagnostic.Issue switch
        {
            DragDropDiagnosticIssue.UacDisabled => _localizationService.T("Settings.DragDropPermission.Detail.UacDisabled"),
            DragDropDiagnosticIssue.PermissionMismatch => _localizationService.T("Settings.DragDropPermission.Detail.PermissionMismatch"),
            DragDropDiagnosticIssue.AppCompatIssue => _localizationService.T("Settings.DragDropPermission.Detail.AppCompatIssue"),
            DragDropDiagnosticIssue.StartupShortcutIssue => _localizationService.T("Settings.DragDropPermission.Detail.StartupShortcutIssue"),
            _ => _localizationService.T("Settings.DragDropPermission.Detail.Ok")
        };
    }
}
