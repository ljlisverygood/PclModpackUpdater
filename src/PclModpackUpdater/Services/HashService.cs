using System.IO;
using System.Security.Cryptography;

namespace PclModpackUpdater.Services;

public static class HashService
{
    public static async Task<string> Sha256FileAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash);
    }
}
