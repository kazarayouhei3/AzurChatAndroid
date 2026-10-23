using System.Security.Cryptography;
using System.Text;

namespace AzurChat.Services;

public class EncryptionService
{
    private const string KeyName = "AzurChat_ApiEncryptionKey";

    /// <summary>
    /// 获取 AES 密钥。
    /// 第一次使用时自动生成，并保存到 SecureStorage。
    /// </summary>
    private async Task<byte[]> GetKeyAsync()
    {
        var savedKey = await SecureStorage.Default.GetAsync(KeyName);

        if (!string.IsNullOrWhiteSpace(savedKey))
        {
            return Convert.FromBase64String(savedKey);
        }

        // AES-256：32字节密钥
        var key = RandomNumberGenerator.GetBytes(32);

        await SecureStorage.Default.SetAsync(
            KeyName,
            Convert.ToBase64String(key));

        return key;
    }

    /// <summary>
    /// 加密 API
    /// </summary>
    public async Task<string> EncryptAsync(string plainText)
    {
        var key = await GetKeyAsync();

        // AES-GCM 推荐使用 12 字节 nonce
        var nonce = RandomNumberGenerator.GetBytes(12);

        var plainBytes = Encoding.UTF8.GetBytes(plainText);

        var cipherBytes = new byte[plainBytes.Length];

        // 认证标签
        var tag = new byte[16];

        using var aes = new AesGcm(key, 16);

        aes.Encrypt(
            nonce,
            plainBytes,
            cipherBytes,
            tag);

        // nonce + tag + 密文一起保存
        return string.Join(".",
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(tag),
            Convert.ToBase64String(cipherBytes));
    }

    /// <summary>
    /// 解密 API
    /// </summary>
    public async Task<string> DecryptAsync(string encryptedText)
    {
        var parts = encryptedText.Split('.');

        if (parts.Length != 3)
        {
            throw new CryptographicException("API 密文格式错误");
        }

        var nonce = Convert.FromBase64String(parts[0]);
        var tag = Convert.FromBase64String(parts[1]);
        var cipherBytes = Convert.FromBase64String(parts[2]);

        var plainBytes = new byte[cipherBytes.Length];

        var key = await GetKeyAsync();

        using var aes = new AesGcm(key, 16);

        aes.Decrypt(
            nonce,
            cipherBytes,
            tag,
            plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}