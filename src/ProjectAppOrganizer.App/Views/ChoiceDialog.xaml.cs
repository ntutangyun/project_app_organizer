using System.Windows;
using System.Windows.Controls;

namespace ProjectAppOrganizer.App.Views;

public partial class ChoiceDialog : Window
{
    /// <summary>Index of the clicked button, or -1 if dismissed.</summary>
    public int Result { get; private set; } = -1;

    public ChoiceDialog(string title, string message, string[] buttons)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;

        for (var i = 0; i < buttons.Length; i++)
        {
            var index = i;
            var button = new Button
            {
                Content = buttons[i],
                Height = 34,
                MinWidth = 96,
                Margin = new Thickness(8, 0, 0, 0),
                // First button is the primary (accent) action; the rest are ghost.
                Style = (Style)FindResource(i == 0 ? "AccentButton" : "GhostButton")
            };
            button.Click += (_, _) => { Result = index; DialogResult = true; };
            ButtonHost.Children.Add(button);
        }

        Loaded += (_, _) => ProjectAppOrganizer.App.Windows.Win32Interop.UseDarkTitleBar(
            new System.Windows.Interop.WindowInteropHelper(this).Handle);
    }

    /// <summary>Shows the dialog and returns the clicked button index (-1 if dismissed).</summary>
    public static int Ask(Window owner, string title, string message, params string[] buttons)
    {
        var dialog = new ChoiceDialog(title, message, buttons) { Owner = owner };
        dialog.ShowDialog();
        return dialog.Result;
    }
}
