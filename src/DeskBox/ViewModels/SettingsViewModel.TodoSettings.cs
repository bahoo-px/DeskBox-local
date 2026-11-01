using System.ComponentModel;
using DeskBox.Features.Todo;

namespace DeskBox.ViewModels;

public partial class SettingsViewModel
{
    private readonly TodoSettingsViewModel _todoSettings;

    // The Todo section's whole binding surface lives on the section editor
    // (batch 47): the shell keeps only the host linkages that run around
    // the editor's writes (the feature-card enable chain and the shell-owned
    // disposal of the editor instance).
    private void OnTodoSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TodoSettingsViewModel.Enabled))
        {
            // The feature-widgets overview row reads the editor's enable
            // state through IsWidgetEnabled.
            OnPropertyChanged(nameof(FeatureWidgetEntries));
        }
    }
}
