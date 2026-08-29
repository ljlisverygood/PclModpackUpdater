using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using PclModpackUpdater.Models;

namespace PclModpackUpdater.Services;

public static class ConfigService
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PclModpackUpdater");

    private static string FilePath => Path.Combine(Folder, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath), JsonOptions) ?? new AppConfig();
            }
        }
        catch
        {
            // 配置文件损坏时回退到默认配置
        }

        return new AppConfig();
    }

    public static void Save(AppConfig config)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(config, JsonOptions));
    }
}
