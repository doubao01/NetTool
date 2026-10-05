using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DeerFlow.WPF.Services;

/// <summary>
/// 敏感信息保护服务接口，负责对密钥、令牌等敏感字段进行加密存储。
/// </summary>
public interface ISecretStore
{
    /// <summary>
    /// 加密明文，返回可安全写入磁盘的密文（Base64）。
    /// 空值返回空字符串。
    /// </summary>
    string Protect(string plainText);

    /// <summary>
    /// 解密密文。若输入不是本机加密的数据（例如历史明文），则原样返回，
    /// 以便兼容旧配置并逐步迁移。
    /// </summary>
    string Unprotect(string cipherText);

    /// <summary>
    /// 判断给定字符串是否为受保护的密文格式。
    /// </summary>
    bool IsProtected(string value);
}

/// <summary>
/// 基于 Windows DPAPI 的敏感信息保护实现。
/// 在非 Windows 平台（如单元测试运行环境）回退到基于机器密钥的 AES 加密，
/// 以保证服务在任意环境都可用且不会明文落盘。
/// </summary>
public sealed class DpapiSecretStore : ISecretStore
{
    private const string ProtectedPrefix = "enc::v1::";

    // 非 Windows 回退方案使用的固定盐值（非机密，仅用于派生密钥）
    private static readonly byte[] FallbackSalt =
        Encoding.UTF8.GetBytes("DeerFlow.WPF.SecretStore.Fallback.v1");

    /// <inheritdoc/>
    public string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectBytes(plainBytes);
        return ProtectedPrefix + Convert.ToBase64String(protectedBytes);
    }

    /// <inheritdoc/>
    public string Unprotect(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return string.Empty;
        }

        if (!IsProtected(cipherText))
        {
            // 兼容历史明文数据：原样返回，由上层在下次保存时重新加密
            return cipherText;
        }

        try
        {
            var payload = Convert.FromBase64String(cipherText[ProtectedPrefix.Length..]);
            var plainBytes = UnprotectBytes(payload);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            // 解密失败（例如跨机器迁移）时不暴露原始密文，返回空值
            return string.Empty;
        }
    }

    /// <inheritdoc/>
    public bool IsProtected(string value)
        => !string.IsNullOrEmpty(value) && value.StartsWith(ProtectedPrefix, StringComparison.Ordinal);

    private static byte[] ProtectBytes(byte[] plainBytes)
    {
        if (OperatingSystem.IsWindows())
        {
#pragma warning disable CA1416 // DPAPI 仅在 Windows 可用，已由运行时检查保护
            return ProtectedData.Protect(plainBytes, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
#pragma warning restore CA1416
        }

        return FallbackProtect(plainBytes);
    }

    private static byte[] UnprotectBytes(byte[] protectedBytes)
    {
        if (OperatingSystem.IsWindows())
        {
#pragma warning disable CA1416
            return ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
#pragma warning restore CA1416
        }

        return FallbackUnprotect(protectedBytes);
    }

    private static byte[] FallbackProtect(byte[] plainBytes)
    {
        using var aes = CreateFallbackAes();
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[aes.IV.Length + cipher.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipher, 0, result, aes.IV.Length, cipher.Length);
        return result;
    }

    private static byte[] FallbackUnprotect(byte[] protectedBytes)
    {
        const int ivLength = 16;
        if (protectedBytes.Length <= ivLength)
        {
            throw new CryptographicException("受保护数据长度不合法");
        }

        using var aes = CreateFallbackAes();
        var iv = new byte[ivLength];
        Buffer.BlockCopy(protectedBytes, 0, iv, 0, ivLength);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(protectedBytes, ivLength, protectedBytes.Length - ivLength);
    }

    private static Aes CreateFallbackAes()
    {
        var aes = Aes.Create();
        aes.Key = DeriveFallbackKey();
        return aes;
    }

    private static byte[] DeriveFallbackKey()
    {
        // 使用机器名 + 用户目录派生密钥，避免把密钥硬编码进产物
        var material = $"{Environment.MachineName}|{Environment.UserName}|DeerFlow.WPF";
        using var kdf = new Rfc2898DeriveBytes(
            Encoding.UTF8.GetBytes(material),
            FallbackSalt,
            iterations: 100_000,
            HashAlgorithmName.SHA256);
        return kdf.GetBytes(32);
    }
}
