using System.Security.Cryptography;

namespace Lanw.Cipher.Cipher;

/// <summary>
/// AES-ECB 无填充加密/解密（由 Nirvana.Cipher.Cipher.AesNoPadding 移植，自研）。
/// 网易鉴权中用于对 loginSeed 与 authToken 做数据加密。
/// </summary>
public static class AesNoPadding
{
    public static byte[] Encrypt(byte[] data, byte[] key, bool encryption = true)
    {
        using var aesNoPadding = Aes.Create();
        aesNoPadding.Key = key;
        aesNoPadding.Mode = CipherMode.ECB;
        aesNoPadding.Padding = PaddingMode.None;

        var transform = encryption ? aesNoPadding.CreateEncryptor() : aesNoPadding.CreateDecryptor();

        return transform.TransformFinalBlock(data, 0, data.Length);
    }
}