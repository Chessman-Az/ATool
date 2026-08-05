using ATool.Services;
using Xunit;

namespace ATool.Tests;

/// <summary>DPAPI 加密往返（本机 Windows 环境全量执行；非 Windows 环境验证 plain 前缀路径）。</summary>
public class KeyProtectionTests
{
    [Fact]
    public void ProtectUnprotect_往返一致()
    {
        const string key = "sk-abcdef1234567890";
        var blob = KeyProtection.Protect(key);
        Assert.NotEqual(System.Text.Encoding.UTF8.GetBytes(key), blob); // 至少不是明文直存
        var round = KeyProtection.Unprotect(blob);
        Assert.Equal(key, round);
    }

    [Fact]
    public void Unprotect_垃圾数据_返回null()
    {
        var r = KeyProtection.Unprotect(new byte[] { 1, 2, 3, 4, 5 });
        if (OperatingSystem.IsWindows())
            Assert.Null(r); // DPAPI 校验失败 → null
        else
            Assert.Null(r); // 无 plain: 前缀 → null
    }
}
