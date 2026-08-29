namespace PclModpackUpdater.Models;

/// <summary>远端更新源信息（来自 version.json 或 HTTP 头）。</summary>
public sealed record RemoteInfo(
    string? Version,
    string? Sha256,
    string? Notes,
    long? SizeBytes,
    string? ETag,
    string? LastModified);

/// <summary>记录在本地的已下载整合包信息（modpack.zip.meta.json）。</summary>
public sealed class InstalledInfo
{
    public string Version { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTimeOffset DownloadedAt { get; set; }
    public string SourceUrl { get; set; } = "";
    public string? ETag { get; set; }
    public string? LastModified { get; set; }
}

/// <summary>检查更新的结果。</summary>
public sealed record CheckOutcome(
    bool Success,
    string Message,
    bool HasUpdate,
    bool ZipExists,
    InstalledInfo? Installed,
    RemoteInfo? Remote);

/// <summary>下载进度。</summary>
public sealed record DownloadProgressReport(
    long ReceivedBytes,
    long? TotalBytes,
    double? SpeedBytesPerSecond,
    string StatusText);
