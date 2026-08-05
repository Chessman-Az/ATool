using System.Security.Cryptography;
using System.Text;

namespace ATool.Services;

/// <summary>
/// API Key 加密存储：Windows 用 DPAPI（CurrentUser 作用域，换用户/机器后无法解密返回 null）；
/// 其他平台存 UTF8 明文 + "plain:" 前缀标记（跨平台编译一致，运行时守卫）。
/// </summary>
public static class KeyProtection
{
    private static readonly byte[] PlainPrefix = "plain:"u8.ToArray();

    public static byte[] Protect(string plain)
    {
        var bytes = Encoding.UTF8.GetBytes(plain);
        if (!OperatingSystem.IsWindows())
        {
            var buf = new byte[PlainPrefix.Length + bytes.Length];
            PlainPrefix.CopyTo(buf, 0);
            bytes.CopyTo(buf, PlainPrefix.Length);
            return buf;
        }
        return ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
    }

    public static string? Unprotect(byte[] blob)
    {
        if (!OperatingSystem.IsWindows())
        {
            if (blob.Length >= PlainPrefix.Length && blob.AsSpan(0, PlainPrefix.Length).SequenceEqual(PlainPrefix))
                return Encoding.UTF8.GetString(blob, PlainPrefix.Length, blob.Length - PlainPrefix.Length);
            return null;
        }
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(blob, null, DataProtectionScope.CurrentUser));
        }
        catch (CryptographicException)
        {
            return null; // 换用户/机器后无法解密 → 界面提示重新添加
        }
    }
}
