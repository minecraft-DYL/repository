using System;

namespace Lanw.Cipher.Extensions;

/// <summary>
/// byte[] 扩展（由 Nirvana.Cipher.Extensions.ByteArrayExtensions 移植，自研）。
/// 原实现使用 C# preview 的 extension 块，此处改为经典的 this 扩展方法，调用点保持一致。
/// </summary>
public static class ByteArrayExtensions
{
    public static byte[] Xor(this byte[] content, byte[] key)
    {
        if (content.Length != key.Length)
        {
            throw new ArgumentException("Key length must be equal to content length.");
        }

        var result = new byte[content.Length];
        for (var i = 0; i < content.Length; i++)
        {
            result[i] = (byte)(content[i] ^ key[i]);
        }

        return result;
    }

    public static byte[] CombineWith(this byte[] content, byte[] second)
    {
        var combined = new byte[content.Length + second.Length];
        Buffer.BlockCopy(content, 0, combined, 0, content.Length);
        Buffer.BlockCopy(second, 0, combined, content.Length, second.Length);
        return combined;
    }
}