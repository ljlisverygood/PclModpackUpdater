using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PclModpackUpdater.Server;

var builder = WebApplication.CreateBuilder(args);

// 整合包可能很大，把请求体上限放宽到 4 GB
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 4L << 30);
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 4L << 30;
    o.ValueLengthLimit = int.MaxValue;
});

// 对外抓取用的 HttpClient：禁止自动重定向，避免通过重定向绕过目标校验
builder.Services.AddHttpClient("fetch").ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AllowAutoRedirect = false,
    ConnectTimeout = TimeSpan.FromSeconds(15),
});

var app = builder.Build();

var dataDir = app.Configuration["DataDir"]
    ?? Environment.GetEnvironmentVariable("PCLUPDATER_DATA")
    ?? Path.Combine(app.Environment.ContentRootPath, "data");
Directory.CreateDirectory(dataDir);

var zipPath = Path.Combine(dataDir, "modpack.zip");
var metaPath = Path.Combine(dataDir, "version.json");
var adminToken = app.Configuration["AdminToken"] ?? Environment.GetEnvironmentVariable("PCLUPDATER_ADMIN_TOKEN");

var jsonOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

VersionInfo? ReadMeta() =>
    File.Exists(metaPath) ? JsonSerializer.Deserialize<VersionInfo>(File.ReadAllText(metaPath), jsonOpts) : null;

void WriteMeta(VersionInfo meta) => File.WriteAllText(metaPath, JsonSerializer.Serialize(meta, jsonOpts));

static async Task<string> Sha256FileAsync(string path)
{
    await using var fs = File.OpenRead(path);
    using var sha = SHA256.Create();
    return Convert.ToHexString(await sha.ComputeHashAsync(fs));
}

IResult? Authorize(HttpRequest request)
{
    if (string.IsNullOrEmpty(adminToken))
    {
        return Results.Json(
            new { error = "服务端未配置 AdminToken（环境变量 PCLUPDATER_ADMIN_TOKEN），管理接口已禁用" },
            statusCode: 503);
    }

    var provided = request.Headers["X-Admin-Token"].ToString();
    if (string.IsNullOrEmpty(provided)
        || !CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(adminToken)))
    {
        return Results.Json(new { error = "未授权" }, statusCode: 401);
    }

    return null;
}

app.MapGet("/", () => Results.Json(new
{
    service = "PclModpackUpdater.Server",
    endpoints = new { version = "/api/version", download = "/api/download" },
}));

// 客户端「检查更新」调用：返回最新版本信息（字段与 version.json 相同）
app.MapGet("/api/version", () =>
{
    var meta = ReadMeta();
    return meta is null
        ? Results.NotFound(new { error = "尚未发布任何版本" })
        : Results.Json(new
        {
            meta.Version,
            meta.Sha256,
            meta.SizeBytes,
            meta.Notes,
            meta.PublishedAt,
            download = "/api/download",
        });
});

// 客户端「下载整合包」调用
app.MapGet("/api/download", () =>
{
    if (!File.Exists(zipPath))
    {
        return Results.NotFound(new { error = "尚未发布任何版本" });
    }

    return Results.File(zipPath, "application/zip", fileDownloadName: "modpack.zip", enableRangeProcessing: true);
});

// 支持 HEAD 探测（部分客户端/工具会用 HEAD 检查文件是否存在）
app.MapMethods("/api/download", new[] { "HEAD" }, () =>
{
    if (!File.Exists(zipPath))
    {
        return Results.NotFound();
    }

    return Results.File(zipPath, "application/zip", enableRangeProcessing: true);
});

// 管理接口：上传新版本 zip（表单字段：file、version（可选）、notes（可选））
app.MapPost("/api/admin/publish", async (
    HttpRequest request,
    IFormFile? file,
    [FromForm] string? version,
    [FromForm] string? notes) =>
{
    if (Authorize(request) is { } denied)
    {
        return denied;
    }

    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "请在表单中上传 zip 文件（字段名 file）" });
    }

    var tmp = Path.Combine(dataDir, $"upload-{Guid.NewGuid():N}.tmp");
    try
    {
        await using (var fs = File.Create(tmp))
        {
            await file.CopyToAsync(fs);
        }

        var meta = new VersionInfo(
            string.IsNullOrWhiteSpace(version) ? DateTimeOffset.Now.ToString("yyyyMMddHHmm") : version.Trim(),
            await Sha256FileAsync(tmp),
            file.Length,
            string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            DateTimeOffset.Now);

        File.Move(tmp, zipPath, overwrite: true);
        WriteMeta(meta);
        return Results.Json(meta);
    }
    finally
    {
        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* 清理失败不影响结果 */ }
    }
}).DisableAntiforgery();

// 管理接口：让服务端从指定 URL 拉取新版本（发布 URL 必须经过 SSRF 校验）
app.MapPost("/api/admin/publish-from-url", async (HttpRequest request, PublishFromUrlRequest? req) =>
{
    if (Authorize(request) is { } denied)
    {
        return denied;
    }

    if (req is null || string.IsNullOrWhiteSpace(req.Url))
    {
        return Results.BadRequest(new { error = "请在 JSON 体中提供 url" });
    }

    if (!UrlGuard.TryValidate(req.Url, out var uri, out var error))
    {
        return Results.BadRequest(new { error });
    }

    var fetcher = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient("fetch");
    fetcher.Timeout = TimeSpan.FromMinutes(30);

    HttpResponseMessage? resp = null;
    var tmp = Path.Combine(dataDir, $"fetch-{Guid.NewGuid():N}.tmp");
    try
    {
        resp = await fetcher.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
        if ((int)resp.StatusCode is 301 or 302 or 303 or 307 or 308)
        {
            return Results.BadRequest(new { error = "目标地址返回重定向，已按策略拒绝" });
        }

        resp.EnsureSuccessStatusCode();

        const long maxBytes = 4L << 30;
        if (resp.Content.Headers.ContentLength is long len && len > maxBytes)
        {
            return Results.BadRequest(new { error = "文件超过 4 GB 上限" });
        }

        long received = 0;
        await using (var src = await resp.Content.ReadAsStreamAsync())
        await using (var fs = File.Create(tmp))
        {
            var buffer = new byte[1 << 16];
            int read;
            while ((read = await src.ReadAsync(buffer)) > 0)
            {
                received += read;
                if (received > maxBytes)
                {
                    throw new InvalidOperationException("文件超过 4 GB 上限");
                }

                await fs.WriteAsync(buffer.AsMemory(0, read));
            }
        }

        var meta = new VersionInfo(
            string.IsNullOrWhiteSpace(req.Version) ? DateTimeOffset.Now.ToString("yyyyMMddHHmm") : req.Version.Trim(),
            await Sha256FileAsync(tmp),
            received,
            string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
            DateTimeOffset.Now);

        File.Move(tmp, zipPath, overwrite: true);
        WriteMeta(meta);
        return Results.Json(meta);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (HttpRequestException ex)
    {
        return Results.BadRequest(new { error = $"下载失败：{ex.Message}" });
    }
    finally
    {
        resp?.Dispose();
        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* 清理失败不影响结果 */ }
    }
});

app.Run();

record VersionInfo(string Version, string Sha256, long SizeBytes, string? Notes, DateTimeOffset PublishedAt);
record PublishFromUrlRequest(string Url, string? Version, string? Notes);
