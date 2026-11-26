using System.Collections.Generic;
using System.Windows;
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.App.Views;

public partial class PickerWindow : Window
{
    public WindowInfo? Selected { get; private set; }

    public PickerWindow(IReadOnlyList<WindowInfo> windows)
    {
        InitializeComponent();
        List.ItemsSource = windows;
        Loaded += (_, _) => ProjectAppOrganizer.App.Themes.ThemeManager.ApplyTitleBar(this);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Selected = List.SelectedItem as WindowInfo;
        if (Selected is null) return;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
