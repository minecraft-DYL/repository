using System.Security.Cryptography;
using System.Text;

namespace Lanw.WPFLauncher.Utils;

/// <summary>
/// MPay / 网易登录编码扩展（由 Nirvana.WPFLauncher.Utils.MPayExtensions 移植，自研）。
/// 原实现使用 C# preview 的 extension 块，此处改为经典的 this 扩展方法，调用点保持一致。
/// </summary>
public static class MPayExtensions
{
    // ---- byte[] ----
    public static string EncodeMd5(this byte[] inputBytes)
    {
        return MD5.HashData(inputBytes).EncodeHex();
    }

    public static string EncodeHex(this byte[] inputBytes)
    {
        return Convert.ToHexString(inputBytes).Replace("-", "").ToLower();
    }

    // ---- string ----
    public static byte[] DecodeHex(this string input)
    {
        return Convert.FromHexString(input);
    }

    public static byte[] EncodeAes(this string input, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        using var encryptor = aes.CreateEncryptor();
        var bytes = Encoding.UTF8.GetBytes(input);
        return encryptor.TransformFinalBlock(bytes, 0, bytes.Length);
    }

    public static string EncodeBase64(this string input)
    {
        return string.IsNullOrEmpty(input) ? string.Empty : Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
    }

    public static string EncodeMd5(this string input)
    {
        return string.IsNullOrEmpty(input) ? string.Empty : Encoding.UTF8.GetBytes(input).EncodeMd5();
    }
}