using System.IO;
using System.Text.Json;
using PclModpackUpdater.Models;

namespace PclModpackUpdater.Services;

/// <summary>读写 modpack.zip 旁的下载记录文件，用于和远端比对版本。</summary>
public static class MarkerService
{
    public const string ZipFileName = "modpack.zip";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string PathFor(string dir) => Path.Combine(dir, ZipFileName + ".meta.json");

    public static InstalledInfo? Read(string dir)
    {
        try
        {
            var path = PathFor(dir);
            if (File.Exists(path))
            {
                return JsonSerializer.Deserialize<InstalledInfo>(File.ReadAllText(path), JsonOptions);
            }
        }
        catch
        {
            // 记录文件损坏时按未安装处理
        }

        return null;
    }

    public static void Write(string dir, InstalledInfo info)
    {
        File.WriteAllText(PathFor(dir), JsonSerializer.Serialize(info, JsonOptions));
    }
}
