using System.Security.Cryptography;
using System.Text;

namespace Lanw.WPFLauncher.Utils.Cipher;

/// <summary>
/// 加密安全随机字符串（由 Nirvana.WPFLauncher.Utils.Cipher.RandomUtil 移植，自研）。
/// </summary>
public static class RandomUtil
{
    public static string GetRandomString(int length, string? chars = null)
    {
        if (length <= 0)
        {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(chars))
        {
            chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghizklmnopqrstuvwxyz0123456789";
        }

        var stringBuilder = new StringBuilder(length);
        var data = new byte[length];
        RandomNumberGenerator.Fill((Span<byte>)data);
        for (var index1 = 0; index1 < length; ++index1)
        {
            var index2 = data[index1] % chars.Length;
            stringBuilder.Append(chars[index2]);
        }

        return stringBuilder.ToString();
    }
}