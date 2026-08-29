using System.IO;

namespace PclModpackUpdater.Services;

public static class PclLauncher
{
    /// <summary>在目录中查找 PCL 启动器主程序（排除本程序自身）。</summary>
    public static string? FindPclExe(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        var self = Environment.ProcessPath;
        var exes = Directory.EnumerateFiles(directory, "*.exe", SearchOption.TopDirectoryOnly)
            .Where(p => !string.Equals(p, self, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return exes.FirstOrDefault(p => Path.GetFileName(p).Contains("pcl", StringComparison.OrdinalIgnoreCase))
            ?? exes.FirstOrDefault();
    }

    /// <summary>启动启动器程序。路径必须真实存在、为 .exe 且位于 PCL 目录内，否则拒绝。</summary>
    public static void Launch(string exePath, string directory)
    {
        var full = Path.GetFullPath(exePath);
        var dir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (!File.Exists(full)
            || !full.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || !full.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("启动器路径校验失败，已阻止启动。");
        }

        LaunchViaShell(full, dir);
    }

    /// <summary>经 Shell.Application 的 ShellExecute 打开程序（与资源管理器双击一致）。</summary>
    private static void LaunchViaShell(string file, string workingDir)
    {
        var type = Type.GetTypeFromProgID("Shell.Application")
            ?? throw new InvalidOperationException("Shell.Application 不可用。");

        dynamic shell = Activator.CreateInstance(type)!;
        shell.ShellExecute(file, null, workingDir, "open", 1);
    }
}
