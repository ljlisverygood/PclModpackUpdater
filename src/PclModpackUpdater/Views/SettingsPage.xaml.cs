using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PclModpackUpdater.Views;

public sealed partial class SettingsPage : Page
{
    private bool _loaded;

    public SettingsPage()
    {
        InitializeComponent();

        ThemeSelector.SelectionChanged += Theme_OnSelectionChanged;
        AutoCheckSwitch.Toggled += AutoCheck_OnToggled;
        LaunchAfterSwitch.Toggled += LaunchAfter_OnToggled;

        ThemeSelector.SelectedIndex = Math.Clamp(App.Config.ThemeIndex, 0, 2);
        AutoCheckSwitch.IsOn = App.Config.AutoCheckOnLaunch;
        LaunchAfterSwitch.IsOn = App.Config.LaunchPclAfterDownload;

        var version = typeof(App).Assembly.GetName().Version;
        AboutText.Text =
            $"PCL 整合包更新器 v{version?.ToString(3) ?? "1.0.0"}\n"
            + "基于 PCL 启动器的 modpack.zip 自动安装机制，实现整合包一键更新。";
        RepoLink.NavigateUri = new Uri(App.RepoUrl);

        _loaded = true;
    }

    private void Theme_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded)
        {
            return;
        }

        App.Config.ThemeIndex = ThemeSelector.SelectedIndex;
        App.SaveConfig();
        if (App.MainWindow is MainWindow main)
        {
            main.ApplyTheme();
        }
    }

    private void AutoCheck_OnToggled(object sender, RoutedEventArgs e)
    {
        if (!_loaded)
        {
            return;
        }

        App.Config.AutoCheckOnLaunch = AutoCheckSwitch.IsOn;
        App.SaveConfig();
    }

    private void LaunchAfter_OnToggled(object sender, RoutedEventArgs e)
    {
        if (!_loaded)
        {
            return;
        }

        App.Config.LaunchPclAfterDownload = LaunchAfterSwitch.IsOn;
        App.SaveConfig();
    }
}
