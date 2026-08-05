namespace ATool.Models;

/// <summary>DeepSeek API Key。密文落库，PlainKey 仅在运行时解密后驻留内存。</summary>
public sealed class ApiKey
{
    public long Id { get; set; }
    public string Alias { get; set; } = "";
    public byte[] EncryptedKey { get; set; } = Array.Empty<byte>();
    public string? LastError { get; set; }
    public string CreatedAt { get; set; } = "";

    /// <summary>运行时解密后的明文（不落库）。</summary>
    public string? PlainKey { get; set; }
}
