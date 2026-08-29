using System.IO;
using System.Net;
using System.Net.Sockets;

namespace PclModpackUpdater.Server;

/// <summary>对外请求的 URL 校验（防 SSRF）：仅允许 http/https；发请求前校验主机，
/// 拒绝 localhost、环回、私有和保留地址。</summary>
public static class UrlGuard
{
    public static bool TryValidate(string? rawUrl, out Uri uri, out string error)
    {
        uri = new Uri("http://invalid.invalid/", UriKind.Absolute);
        error = "";

        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            error = "URL 不能为空";
            return false;
        }

        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            error = "仅允许 http/https 地址";
            return false;
        }

        if (parsed.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
        {
            if (!IPAddress.TryParse(parsed.Host, out var literal) || !IsAllowedIp(literal))
            {
                error = "禁止访问私有或保留地址";
                return false;
            }
        }
        else
        {
            var host = parsed.Host.TrimEnd('.');
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            {
                error = "禁止访问 localhost";
                return false;
            }

            IPAddress[] addresses;
            try
            {
                addresses = Dns.GetHostEntryAsync(host).GetAwaiter().GetResult().AddressList;
            }
            catch
            {
                error = "无法解析主机名";
                return false;
            }

            if (addresses.Length == 0 || addresses.Any(a => !IsAllowedIp(a)))
            {
                error = "主机解析到了私有或保留地址";
                return false;
            }
        }

        uri = parsed;
        return true;
    }

    private static bool IsAllowedIp(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
        {
            return IsAllowedIp(ip.MapToIPv4());
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast
                || IPAddress.IPv6Loopback.Equals(ip) || IPAddress.IPv6Any.Equals(ip) || IPAddress.IPv6None.Equals(ip))
            {
                return false;
            }

            // fc00::/7 唯一本地地址（ULA）
            var v6 = ip.GetAddressBytes();
            return (v6[0] & 0xFE) != 0xFC;
        }

        if (IPAddress.Loopback.Equals(ip) || IPAddress.Any.Equals(ip) || IPAddress.Broadcast.Equals(ip))
        {
            return false;
        }

        var b = ip.GetAddressBytes();
        return b[0] switch
        {
            0 => false,                            // 0.0.0.0/8 本网络
            10 => false,                           // 10.0.0.0/8 私有
            100 => b[1] is < 64 or > 127,          // 100.64.0.0/10 运营商级 NAT
            127 => false,                          // 127.0.0.0/8 环回
            169 => b[1] != 254,                    // 169.254.0.0/16 链路本地
            172 => b[1] is < 16 or > 31,           // 172.16.0.0/12 私有
            192 => b[1] != 168 && b[1] != 0,       // 192.168.0.0/16 与 192.0.0.0/24 等保留段
            198 => b[1] is not (18 or 19),         // 198.18.0.0/15 基准测试
            224 => false,                          // 224.0.0.0/4 组播
            240 => false,                          // 240.0.0.0/4 保留
            255 => false,                          // 255.255.255.255 广播
            _ => true,
        };
    }
}
