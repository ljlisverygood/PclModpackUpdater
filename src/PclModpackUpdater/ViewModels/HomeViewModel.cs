using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PclModpackUpdater.Models;
using PclModpackUpdater.Services;

namespace PclModpackUpdater.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly UpdateService _updateService = new();
    private CancellationTokenSource? _downloadCts;

    [ObservableProperty] private string urlsText = "";
    [ObservableProperty] private string versionUrlText = "";
    [ObservableProperty] private string pclDirectoryText = AppContext.BaseDirectory;

    [ObservableProperty] private string statusTitle = "就绪";
    [ObservableProperty] private string statusMessage = "填好更新源后，点「检查更新」开始。";
    [ObservableProperty] private InfoBarSeverity statusSeverity = InfoBarSeverity.Informational;
    [ObservableProperty] private bool isStatusOpen;

    [ObservableProperty] private bool isChecking;
    [ObservableProperty] private bool isDownloading;
    [ObservableProperty] private bool hasTotal;
    [ObservableProperty] private double downloadPercent;
    [ObservableProperty] private string progressText = "尚未开始下载";

    [ObservableProperty] private string localStateText = "正在读取…";

    public bool IsIdle => !IsChecking && !IsDownloading;

    public Visibility CancelVisibility => IsDownloading ? Visibility.Visible : Visibility.Collapsed;

    public bool IsIndeterminate => IsDownloading && !HasTotal;

    partial void OnIsCheckingChanged(bool value) => OnPropertyChanged(nameof(IsIdle));

    partial void OnIsDownloadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(CancelVisibility));
        OnPropertyChanged(nameof(IsIndeterminate));
    }

    partial void OnHasTotalChanged(bool value) => OnPropertyChanged(nameof(IsIndeterminate));

    public void Initialize()
    {
        var config = App.Config;
        UrlsText = string.Join(Environment.NewLine, config.DownloadUrls);
        VersionUrlText = config.VersionUrl ?? "";
        PclDirectoryText = string.IsNullOrWhiteSpace(config.PclDirectory) ? AppContext.BaseDirectory : config.PclDirectory;
        RefreshLocalState();
    }

    public async Task InitializeWithAutoCheckAsync()
    {
        Initialize();
        if (App.Config.AutoCheckOnLaunch)
        {
            await CheckCommand.ExecuteAsync(null);
        }
    }

    public void SaveToConfig()
    {
        var config = App.Config;
        config.DownloadUrls = UrlsText.Split('\n', '\r')
            .Select(u => u.Trim())
            .Where(u => u.Length > 0)
            .ToList();
        config.VersionUrl = string.IsNullOrWhiteSpace(VersionUrlText) ? null : VersionUrlText.Trim();
        config.PclDirectory = string.IsNullOrWhiteSpace(PclDirectoryText) ? AppContext.BaseDirectory : PclDirectoryText.Trim();
        App.SaveConfig();
    }

    public void RefreshLocalState()
    {
        var dir = PclDirectoryText.Trim();
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            LocalStateText = "PCL 目录未设置或不存在。";
            return;
        }

        var lines = new List<string> { $"PCL 目录：{dir}" };
        var zipPath = Path.Combine(dir, MarkerService.ZipFileName);

        lines.Add(File.Exists(zipPath)
            ? $"modpack.zip：已存在，{FormatSize(new FileInfo(zipPath).Length)}"
            : "modpack.zip：尚未下载");

        if (MarkerService.Read(dir) is { } installed)
        {
            var version = string.IsNullOrWhiteSpace(installed.Version) ? "未知版本" : installed.Version;
            lines.Add($"上次下载：{version}，{installed.DownloadedAt:yyyy-MM-dd HH:mm}");
        }

        lines.Add(PclLauncher.FindPclExe(dir) is { } pcl
            ? $"检测到启动器：{Path.GetFileName(pcl)}"
            : "未检测到启动器（PCL.exe），但不影响下载");

        LocalStateText = string.Join('\n', lines);
    }

    private void ShowStatus(string title, string message, InfoBarSeverity severity)
    {
        StatusTitle = title;
        StatusMessage = message;
        StatusSeverity = severity;
        IsStatusOpen = true;
    }

    [RelayCommand]
    private async Task CheckAsync(CancellationToken ct)
    {
        SaveToConfig();
        IsChecking = true;
        try
        {
            var result = await _updateService.CheckAsync(App.Config, ct);
            if (result.Success)
            {
                ShowStatus(
                    result.HasUpdate ? "有更新" : "已是最新",
                    result.Message,
                    result.HasUpdate ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
            }
            else
            {
                ShowStatus("检查失败", result.Message, InfoBarSeverity.Error);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ShowStatus("检查失败", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            IsChecking = false;
            RefreshLocalState();
        }
    }

    [RelayCommand]
    private async Task DownloadAsync(CancellationToken ct)
    {
        SaveToConfig();
        _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        IsDownloading = true;
        HasTotal = false;
        DownloadPercent = 0;
        ProgressText = "准备下载…";

        var progress = new Progress<DownloadProgressReport>(ReportProgress);
        try
        {
            var installed = await _updateService.DownloadAsync(App.Config, progress, _downloadCts.Token);
            DownloadPercent = 100;
            ProgressText = $"完成，共 {FormatSize(installed.SizeBytes)}";
            ShowStatus("下载完成", "modpack.zip 已就绪，启动 PCL 后会自动提示安装整合包。", InfoBarSeverity.Success);

            if (App.Config.LaunchPclAfterDownload && PclLauncher.FindPclExe(App.Config.PclDirectory) is { } pcl)
            {
                PclLauncher.Launch(pcl, App.Config.PclDirectory);
            }
        }
        catch (OperationCanceledException)
        {
            ShowStatus("已取消", "下载已取消。", InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            ShowStatus("下载失败", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            IsDownloading = false;
            _downloadCts.Dispose();
            _downloadCts = null;
            RefreshLocalState();
        }
    }

    [RelayCommand]
    private void CancelDownload() => _downloadCts?.Cancel();

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        var dir = PclDirectoryText.Trim();
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            ShowStatus("无法打开", "PCL 目录不存在。", InfoBarSeverity.Error);
            return;
        }

        var launched = await Windows.System.Launcher.LaunchFolderPathAsync(dir);
        if (!launched)
        {
            ShowStatus("无法打开", "系统拒绝了打开文件夹的请求。", InfoBarSeverity.Error);
        }
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop,
            };
            picker.FileTypeFilter.Add("*");

            if (App.MainWindow is { } window)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            if (await picker.PickSingleFolderAsync() is { } folder)
            {
                PclDirectoryText = folder.Path;
                SaveToConfig();
                RefreshLocalState();
            }
        }
        catch (Exception ex)
        {
            // 未打包应用可能不支持系统文件夹选择器，退回手动输入
            ShowStatus("无法打开文件夹选择器", $"{ex.Message}。可直接在上方输入框粘贴 PCL 目录路径。", InfoBarSeverity.Warning);
        }
    }

    [RelayCommand]
    private void LaunchPcl()
    {
        try
        {
            if (PclLauncher.FindPclExe(PclDirectoryText.Trim()) is not { } pcl)
            {
                ShowStatus("未找到 PCL", "目录中没有可执行文件，请确认选择的是 PCL.exe 所在文件夹。", InfoBarSeverity.Error);
                return;
            }

                PclLauncher.Launch(pcl, PclDirectoryText.Trim());
        }
        catch (Exception ex)
        {
            ShowStatus("启动失败", ex.Message, InfoBarSeverity.Error);
        }
    }

    private void ReportProgress(DownloadProgressReport report)
    {
        if (report.TotalBytes is long total && total > 0)
        {
            DownloadPercent = report.ReceivedBytes * 100.0 / total;
            if (!HasTotal)
            {
                HasTotal = true;
            }
            ProgressText = $"{FormatSize(report.ReceivedBytes)} / {FormatSize(total)}{SpeedSuffix(report)}";
        }
        else
        {
            ProgressText = $"已下载 {FormatSize(report.ReceivedBytes)}{SpeedSuffix(report)}";
        }
    }

    private static string SpeedSuffix(DownloadProgressReport report) =>
        report.SpeedBytesPerSecond is double s && s > 0 ? $" · {FormatSize((long)s)}/s" : "";

    internal static string FormatSize(long bytes) => bytes switch
    {
        >= 1 << 30 => $"{bytes / 1073741824.0:0.00} GB",
        >= 1 << 20 => $"{bytes / 1048576.0:0.0} MB",
        >= 1 << 10 => $"{bytes / 1024.0:0.0} KB",
        _ => $"{bytes} B",
    };
}
