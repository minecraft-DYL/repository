using System;
using System.IO;
using System.IO.Hashing;
using System.Security.Cryptography;
using System.Text;
using Lanw.Cipher.Cipher;

namespace Lanw.Cipher.Extensions;

/// <summary>
/// Yggdrasil 加解密/序列化扩展（由 Nirvana.Cipher.Extensions.YggdrasilExtensions 移植，自研）。
/// 原实现使用 C# preview 的 extension 块，此处改为经典的 this 扩展方法，调用点保持一致。
/// </summary>
public static class YggdrasilExtensions
{
    // ---- byte[] ----
    public static byte[] EncodeSha256(this byte[] input)
    {
        return SHA256.HashData(input);
    }

    // ---- int ----
    public static byte[] ToByteArray(this int value, bool littleEndian = true)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian != littleEndian) Array.Reverse(bytes);

        return bytes;
    }

    // ---- long ----
    public static byte[] ToByteArray(this long value, bool littleEndian = true)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian != littleEndian) Array.Reverse(bytes);

        return bytes;
    }

    public static byte[] ToShortByteArray(this long value, bool littleEndian = true)
    {
        var bytes = BitConverter.GetBytes((short)value);
        if (BitConverter.IsLittleEndian != littleEndian) Array.Reverse(bytes);

        return bytes;
    }

    private static byte[] ToShortByteArray(this int value, bool littleEndian = true)
    {
        var bytes = BitConverter.GetBytes((short)value);
        if (BitConverter.IsLittleEndian != littleEndian)
        {
            Array.Reverse(bytes);
        }

        return bytes;
    }

    // ---- ChaChaPacker ----
    public static byte[] PackMessage(this ChaChaPacker packer, byte type, byte[] data)
    {
        var message = new byte[data.Length + 10];
        var length = BitConverter.GetBytes((short)(message.Length - 2));
        Array.Copy(length, 0, message, 0, 2);

        message[6] = type;
        message[7] = 136;
        message[8] = 136;
        message[9] = 136;
        Array.Copy(data, 0, message, 10, data.Length);

        var crc32 = Crc32.Hash(message.AsSpan(6));
        Array.Copy(crc32, 0, message, 2, 4);

        packer.ProcessBytes(message, 2, message.Length - 2, message, 2);
        return message;
    }

    public static (byte, byte[]) UnpackMessage(this ChaChaPacker packer, byte[] data)
    {
        packer.ProcessBytes(data, 0, data.Length, data, 0);

        var crc32Data = new byte[4];
        Crc32.Hash(data.AsSpan(4, data.Length - 4), crc32Data);

        for (var i = 0; i < 4; i++)
            if (crc32Data[i] != data[i])
                throw new Exception("Unpacking failed");

        var result = new byte[data.Length - 8];
        Array.Copy(data, 8, result, 0, result.Length);
        return (data[4], result);
    }

    // ---- MemoryStream ----
    public static void WriteInt(this MemoryStream stream, int value, bool littleEndian = true)
    {
        var bytes = value.ToByteArray(littleEndian);
        stream.Write(bytes);
    }

    public static void WriteShort(this MemoryStream stream, int value, bool littleEndian = true)
    {
        var bytes = value.ToShortByteArray(littleEndian);
        stream.Write(bytes);
    }

    public static void WriteLong(this MemoryStream stream, long value, bool littleEndian = true)
    {
        var bytes = value.ToByteArray(littleEndian);
        stream.Write(bytes);
    }

    public static void WriteBytes(this MemoryStream stream, byte[] data)
    {
        stream.Write(data);
    }

    public static void WriteString(this MemoryStream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        stream.WriteByte((byte)bytes.Length);
        stream.Write(bytes);
    }

    public static void WriteByteLengthString(this MemoryStream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        stream.WriteByte((byte)bytes.Length);
        stream.Write(bytes);
    }

    public static void WriteShortString(this MemoryStream stream, string value, bool littleEndian = true)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        stream.WriteShort(bytes.Length, littleEndian);
        stream.Write(bytes);
    }

    public static void WriteShortBytes(this MemoryStream stream, byte[] data, bool littleEndian = true)
    {
        stream.WriteShort(data.Length, littleEndian);
        stream.Write(data);
    }
}