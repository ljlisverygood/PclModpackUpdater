using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using PclModpackUpdater.Views;

namespace PclModpackUpdater;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Title = "PCL 整合包更新器";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SystemBackdrop = new MicaBackdrop();

        ApplyTheme();

        Nav.SelectedItem = Nav.MenuItems[0];
    }

    private void Nav_OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem { Tag: string tag })
        {
            return;
        }

        var page = tag switch
        {
            "settings" => typeof(SettingsPage),
            _ => typeof(HomePage),
        };
        ContentFrame.Navigate(page, null, new SlideNavigationTransitionInfo());
    }

    public void ApplyTheme()
    {
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = App.Config.ThemeIndex switch
            {
                1 => ElementTheme.Light,
                2 => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
        }
    }
}
