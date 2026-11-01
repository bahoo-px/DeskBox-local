using System.Globalization;
using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskBox.Helpers;
using DeskBox.Models;
using DeskBox.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DeskBox.ViewModels;

public partial class SettingsViewModel
{
    public string GetWidgetCollapseBehaviorDisplayName(string behavior)
    {
        return WidgetCollapseBehaviorNames.Normalize(behavior) switch
        {
            WidgetCollapseBehavior.Expanded => _localizationService.T("Settings.CollapseBehavior.Expanded"),
            WidgetCollapseBehavior.Smart => _localizationService.T("Settings.CollapseBehavior.Smart"),
            _ => _localizationService.T("Settings.CollapseBehavior.Click")
        };
    }

    public string GetHoverButtonActionDisplayName(string action)
    {
        return action switch
        {
            SettingsService.WidgetHoverActionLockPosition => _localizationService.T("Settings.HoverButtonActions.LockPosition"),
            SettingsService.WidgetHoverActionLockSize => _localizationService.T("Settings.HoverButtonActions.LockSize"),
            SettingsService.WidgetHoverActionAdd => _localizationService.T("Settings.HoverButtonActions.Add"),
            SettingsService.WidgetHoverActionMore => _localizationService.T("Settings.HoverButtonActions.More"),
            SettingsService.WidgetHoverActionDelete => _localizationService.T("Settings.HoverButtonActions.Delete"),
            _ => action
        };
    }

    public string GetLanguageDisplayName(string language)
    {
        return _localizationService.GetLanguageDisplayName(language);
    }
}
