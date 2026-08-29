using System.IO;
using System.Text.Json;
using PclModpackUpdater.Models;

namespace PclModpackUpdater.Services;

/// <summary>负责检查更新源并把最新整合包下载为 PCL 目录下的 modpack.zip。
/// 支持两种更新源写法：后端服务地址（http://主机:端口，自动使用 /api/version 与 /api/download），
/// 或 zip 文件直链（可配合可选的 version.json）。</summary>
public sealed class UpdateService
{
    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(15),
        })
        {
            // 大整合包下载耗时可能较长
            Timeout = TimeSpan.FromMinutes(30),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PclModpackUpdater/1.0");
        return client;
    }

    private sealed record Endpoint(bool IsBackend, string InfoUrl, string DownloadUrl);

    /// <summary>裸地址（仅协议+主机+端口）视为后端服务，自动展开为 API 端点。</summary>
    private static bool TryResolve(string url, out Endpoint endpoint)
    {
        endpoint = new Endpoint(false, url, url);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        if (parsed.AbsolutePath is "/" or "")
        {
            endpoint = new Endpoint(
                true,
                new Uri(parsed, "api/version").ToString(),
                new Uri(parsed, "api/download").ToString());
        }

        return true;
    }

    public static IReadOnlyList<string> GetUrls(AppConfig config) =>
        config.DownloadUrls.Select(u => u.Trim()).Where(u => u.Length > 0).Distinct().ToList();

    public async Task<CheckOutcome> CheckAsync(AppConfig config, CancellationToken ct = default)
    {
        var urls = GetUrls(config);
        if (urls.Count == 0)
        {
            return new CheckOutcome(false, "请先填写更新源地址（后端服务地址或 zip 直链）。", false, false, null, null);
        }

        var dir = config.PclDirectory;
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            return new CheckOutcome(false, "PCL 目录不存在，请先选择 Plain Craft Launcher.exe 所在文件夹。", false, false, null, null);
        }

        var installed = MarkerService.Read(dir);
        var zipExists = File.Exists(Path.Combine(dir, MarkerService.ZipFileName));
        var remote = await GetRemoteInfoAsync(config, ct);

        if (remote is null)
        {
            return new CheckOutcome(false, "无法连接更新源，请检查网络或地址（后端地址或 zip 直链）。", false, zipExists, installed, null);
        }

        var hasUpdate = NeedsUpdate(installed, zipExists, remote);
        var message = hasUpdate switch
        {
            true when !zipExists => "未找到 modpack.zip，点击「一键更新」开始安装。",
            true => $"发现新版本{(string.IsNullOrEmpty(remote.Version) ? "" : $"：{remote.Version}")}，点击「一键更新」开始下载。",
            false => $"已是最新版本{(string.IsNullOrEmpty(installed?.Version) ? "" : $"（{installed.Version}）")}。",
        };

        return new CheckOutcome(true, message, hasUpdate, zipExists, installed, remote);
    }

    /// <summary>把最新整合包下载为 dir/modpack.zip（先写 .part 临时文件，校验后再落盘）。</summary>
    public async Task<InstalledInfo> DownloadAsync(
        AppConfig config, IProgress<DownloadProgressReport> progress, CancellationToken ct)
    {
        var urls = GetUrls(config);
        if (urls.Count == 0)
        {
            throw new InvalidOperationException("请先填写更新源地址（后端服务地址或 zip 直链）。");
        }

        var dir = config.PclDirectory;
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            throw new InvalidOperationException("PCL 目录不存在，请先选择 Plain Craft Launcher.exe 所在文件夹。");
        }

        Directory.CreateDirectory(dir);

        var remote = await TryGetRemoteAsync(config, ct);
        var expectedSha = remote?.Sha256;
        var version = remote?.Version ?? "";

        var zipPath = Path.Combine(dir, MarkerService.ZipFileName);
        var partPath = zipPath + ".part";

        Exception? lastError = null;
        foreach (var url in urls)
        {
            if (!TryResolve(url, out var endpoint))
            {
                lastError = new InvalidOperationException($"无效的更新源地址：{url}");
                continue;
            }

            try
            {
                var downloaded = await DownloadFileAsync(endpoint.DownloadUrl, partPath, progress, ct);

                if (!string.IsNullOrWhiteSpace(expectedSha)
                    && !string.Equals(downloaded.Sha256, expectedSha, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("下载文件的 SHA256 与版本信息不一致，已中止安装。");
                }

                File.Move(partPath, zipPath, true);

                var installed = new InstalledInfo
                {
                    Version = version,
                    Sha256 = downloaded.Sha256,
                    SizeBytes = downloaded.SizeBytes,
                    DownloadedAt = DateTimeOffset.Now,
                    SourceUrl = url,
                    ETag = downloaded.ETag,
                    LastModified = downloaded.LastModified,
                };
                MarkerService.Write(dir, installed);
                return installed;
            }
            catch (OperationCanceledException)
            {
                TryDelete(partPath);
                throw;
            }
            catch (Exception ex)
            {
                TryDelete(partPath);
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"所有更新源均失败，最后一个错误：{lastError?.Message}", lastError);
    }

    private async Task<RemoteInfo?> TryGetRemoteAsync(AppConfig config, CancellationToken ct)
    {
        try
        {
            return await GetRemoteInfoAsync(config, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // 下载前探测失败不阻塞下载
            return null;
        }
    }

    /// <summary>优先读 version.json；否则遍历更新源：后端地址读 /api/version，直链做 HEAD 探测。</summary>
    private async Task<RemoteInfo?> GetRemoteInfoAsync(AppConfig config, CancellationToken ct)
    {
        var versionUrl = config.VersionUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(versionUrl))
        {
            try
            {
                using var resp = await Http.GetAsync(versionUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();
                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                return await ParseVersionJsonAsync(stream, ct,
                    resp.Headers.ETag?.Tag, resp.Content.Headers.LastModified?.ToString("R"));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // version.json 获取失败时回退到更新源探测
            }
        }

        foreach (var url in GetUrls(config))
        {
            if (!TryResolve(url, out var endpoint))
            {
                continue;
            }

            try
            {
                if (endpoint.IsBackend)
                {
                    using var resp = await Http.GetAsync(endpoint.InfoUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                    resp.EnsureSuccessStatusCode();
                    await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                    return await ParseVersionJsonAsync(stream, ct,
                        resp.Headers.ETag?.Tag, resp.Content.Headers.LastModified?.ToString("R"));
                }

                using var request = new HttpRequestMessage(HttpMethod.Head, endpoint.DownloadUrl);
                using var head = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                head.EnsureSuccessStatusCode();
                return new RemoteInfo(
                    null,
                    null,
                    null,
                    head.Content.Headers.ContentLength,
                    head.Headers.ETag?.Tag,
                    head.Content.Headers.LastModified?.ToString("R"));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // 尝试下一个更新源
            }
        }

        return null;
    }

    /// <summary>解析 version.json / 后端 /api/version 的响应（字段：version、sha256、sizeBytes、notes）。</summary>
    private static async Task<RemoteInfo> ParseVersionJsonAsync(
        Stream stream, CancellationToken ct, string? etag, string? lastModified)
    {
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        string? GetStr(string name) =>
            root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;
        long? GetLong(string name) =>
            root.TryGetProperty(name, out var el) && el.TryGetInt64(out var v) ? v : null;

        return new RemoteInfo(
            GetStr("version"),
            GetStr("sha256"),
            GetStr("notes"),
            GetLong("sizeBytes"),
            etag,
            lastModified);
    }

    private static bool NeedsUpdate(InstalledInfo? installed, bool zipExists, RemoteInfo remote)
    {
        if (!zipExists || installed is null)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(remote.Sha256))
        {
            return !string.Equals(installed.Sha256, remote.Sha256, StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrWhiteSpace(remote.Version))
        {
            return !string.Equals(installed.Version, remote.Version, StringComparison.Ordinal);
        }

        if (remote.ETag is not null || remote.LastModified is not null)
        {
            return !string.Equals(installed.ETag, remote.ETag, StringComparison.Ordinal)
                || !string.Equals(installed.LastModified, remote.LastModified, StringComparison.Ordinal);
        }

        if (remote.SizeBytes is long size)
        {
            return size != installed.SizeBytes;
        }

        // 无法判断时按有更新处理，让用户自行决定是否下载
        return true;
    }

    private sealed record DownloadedFileInfo(
        string Sha256, long SizeBytes, string? ETag, string? LastModified);

    private static async Task<DownloadedFileInfo> DownloadFileAsync(
        string url, string partPath, IProgress<DownloadProgressReport> progress, CancellationToken ct)
    {
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        long? total = resp.Content.Headers.ContentLength;
        string? etag = resp.Headers.ETag?.Tag;
        string? lastModified = resp.Content.Headers.LastModified?.ToString("R");

        long received = 0;
        long lastReportBytes = 0;
        var lastReportTime = DateTime.UtcNow;
        double? speed = null;

        await using (var source = await resp.Content.ReadAsStreamAsync(ct))
        await using (var target = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
        {
            var buffer = new byte[1 << 16];
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
                received += read;

                var now = DateTime.UtcNow;
                if ((now - lastReportTime).TotalMilliseconds >= 200)
                {
                    var seconds = (now - lastReportTime).TotalSeconds;
                    if (seconds > 0)
                    {
                        speed = (received - lastReportBytes) / seconds;
                    }
                    lastReportTime = now;
                    lastReportBytes = received;
                    progress.Report(new DownloadProgressReport(received, total, speed, "正在下载"));
                }
            }
        }

        progress.Report(new DownloadProgressReport(received, total ?? received, speed, "正在计算校验值"));
        var sha = await HashService.Sha256FileAsync(partPath, ct);
        return new DownloadedFileInfo(sha, received, etag, lastModified);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 清理临时文件失败不影响主流程
        }
    }
}
