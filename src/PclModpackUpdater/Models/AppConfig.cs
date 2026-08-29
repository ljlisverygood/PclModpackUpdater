namespace PclModpackUpdater.Models;

public class AppConfig
{
    /// <summary>PCL 所在目录，modpack.zip 会下载到这里。本程序可安装在任意位置，首次使用时选择。</summary>
    public string PclDirectory { get; set; } = "";

    /// <summary>内置默认更新源（后端服务地址，可在界面修改）。</summary>
    public const string DefaultUpdateSource = "http://8.137.194.109:12458";

    /// <summary>更新源：后端服务地址或 zip 直链，可填多个镜像，每行一个，按顺序尝试。</summary>
    public List<string> DownloadUrls { get; set; } = new() { DefaultUpdateSource };

    /// <summary>可选的版本清单（version.json）地址，用于避免重复下载整个整合包。</summary>
    public string? VersionUrl { get; set; }

    public bool AutoCheckOnLaunch { get; set; } = true;

    public bool LaunchPclAfterDownload { get; set; }

    /// <summary>0 = 跟随系统，1 = 浅色，2 = 深色。</summary>
    public int ThemeIndex { get; set; }
}
