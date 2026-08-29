using Microsoft.UI.Xaml;
using PclModpackUpdater.Models;
using PclModpackUpdater.Services;

namespace PclModpackUpdater;

public partial class App : Application
{
    public const string RepoUrl = "https://github.com/ljlisverygood/PclModpackUpdater";

    public static Window? MainWindow { get; set; }

    public static AppConfig Config { get; private set; } = new();

    public App()
    {
        InitializeComponent();
        Config = ConfigService.Load();

        // 兼容旧配置里更新源为空的情况：回退到内置更新源
        if (Config.DownloadUrls.Count == 0)
        {
            Config.DownloadUrls = new() { AppConfig.DefaultUpdateSource };
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }

    public static void SaveConfig() => ConfigService.Save(Config);
}
