using System.ComponentModel;
using System.Runtime.InteropServices;
using Wyolm.Core.Models;

namespace Wyolm.Core.Interop;

/// <summary>重解析点的读写底层实现。</summary>
internal static class ReparsePoint
{
    /// <summary>读取路径的重解析标签，用于区分 Junction / Symlink / 普通目录。</summary>
    public static LinkKind GetLinkKind(string path)
    {
        var attrs = NativeMethods.GetFileAttributes(NativeMethods.ToLongPath(path));
        if (attrs == NativeMethods.INVALID_FILE_ATTRIBUTES)
            return LinkKind.None;
        if ((attrs & (uint)FileAttributes.ReparsePoint) == 0)
            return LinkKind.None;

        var tag = ReadReparseTag(path);
        return tag switch
        {
            NativeMethods.IO_REPARSE_TAG_MOUNT_POINT => LinkKind.Junction,
            NativeMethods.IO_REPARSE_TAG_SYMLINK => LinkKind.SymbolicLink,
            _ => LinkKind.None,
        };
    }

    private static uint ReadReparseTag(string path)
    {
        using var find = NativeMethods.FindFirstFile(NativeMethods.ToLongPath(path.TrimEnd('\\')), out var data);
        if (find.IsInvalid) return 0;
        return data.dwReserved0;
    }

    /// <summary>
    /// 创建一个目录联接。不需要管理员权限，不需要开发者模式。
    /// </summary>
    /// <param name="linkPath">要创建的链接路径（必须不存在）。</param>
    /// <param name="targetPath">链接指向的真实目录（必须存在）。</param>
    public static void CreateJunction(string linkPath, string targetPath)
    {
        var substitute = NativeMethods.ToNtPath(targetPath);
        var print = targetPath;

        var substituteBytes = System.Text.Encoding.Unicode.GetBytes(substitute);
        var printBytes = System.Text.Encoding.Unicode.GetBytes(print);

        // REPARSE_DATA_BUFFER 中 MountPointReparseBuffer 的布局：
        // SubstituteNameOffset / SubstituteNameLength / PrintNameOffset / PrintNameLength / PathBuffer
        int pathBufferLength = substituteBytes.Length + 2 + printBytes.Length + 2;
        int reparseDataLength = 8 + pathBufferLength;

        Directory.CreateDirectory(linkPath);

        using var handle = NativeMethods.CreateFile(
            NativeMethods.ToLongPath(linkPath),
            NativeMethods.GENERIC_WRITE,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE | NativeMethods.FILE_SHARE_DELETE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            NativeMethods.FILE_FLAG_OPEN_REPARSE_POINT | NativeMethods.FILE_FLAG_BACKUP_SEMANTICS,
            IntPtr.Zero);

        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"无法打开目录以设置联接点: {linkPath}");

        // FSCTL_SET_REPARSE_POINT 要求输入缓冲区大小**恰好**是 8 + ReparseDataLength，
        // 多给一个字节都会得到 ERROR_INVALID_REPARSE_DATA (4392)。
        var buffer = new byte[8 + reparseDataLength];
        using (var ms = new MemoryStream(buffer))
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(NativeMethods.IO_REPARSE_TAG_MOUNT_POINT);
            bw.Write((ushort)reparseDataLength);
            bw.Write((ushort)0);            // Reserved
            bw.Write((ushort)0);            // SubstituteNameOffset
            bw.Write((ushort)substituteBytes.Length);
            bw.Write((ushort)(substituteBytes.Length + 2)); // PrintNameOffset
            bw.Write((ushort)printBytes.Length);
            bw.Write(substituteBytes);
            bw.Write((ushort)0);
            bw.Write(printBytes);
            bw.Write((ushort)0);
        }

        if (!NativeMethods.DeviceIoControl(handle, NativeMethods.FSCTL_SET_REPARSE_POINT,
                buffer, buffer.Length, null, 0, out _, IntPtr.Zero))
        {
            var err = Marshal.GetLastWin32Error();
            try { Directory.Delete(linkPath, false); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw new Win32Exception(err, $"创建目录联接失败: {linkPath} -> {targetPath}");
        }
    }

    /// <summary>
    /// 创建一个目录符号链接。Windows 10 1703+ 在开启开发者模式后无需管理员权限。
    /// </summary>
    public static void CreateSymbolicLink(string linkPath, string targetPath)
    {
        uint flags = NativeMethods.SYMBOLIC_LINK_FLAG_DIRECTORY | NativeMethods.SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE;

        if (!NativeMethods.CreateSymbolicLink(NativeMethods.ToLongPath(linkPath), NativeMethods.ToLongPath(targetPath), flags))
        {
            var err = Marshal.GetLastWin32Error();

            // 某些系统不接受 ALLOW_UNPRIVILEGED_CREATE 标志，回退到传统调用。
            if (!NativeMethods.CreateSymbolicLink(NativeMethods.ToLongPath(linkPath), NativeMethods.ToLongPath(targetPath),
                    NativeMethods.SYMBOLIC_LINK_FLAG_DIRECTORY))
            {
                throw new Win32Exception(err, $"创建符号链接失败: {linkPath} -> {targetPath}");
            }
        }
    }

    /// <summary>
    /// 读取链接目标。使用 .NET 内建实现，它在 Windows 上能正确解析 Junction 和 Symlink。
    /// </summary>
    public static string? ReadLinkTarget(string path)
    {
        try
        {
            var info = new DirectoryInfo(path);
            if (info.LinkTarget is { Length: > 0 } target)
                return NormalizeTarget(path, target);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return null;
    }

    private static string NormalizeTarget(string linkPath, string target)
    {
        // Junction 的 LinkTarget 可能是 "\??\D:\foo" 形式。
        if (target.StartsWith(NativeMethods.NtPathPrefix, StringComparison.Ordinal))
            target = target[NativeMethods.NtPathPrefix.Length..];
        else if (target.StartsWith(@"\??\UNC\", StringComparison.Ordinal))
            target = @"\\" + target[8..];

        if (target.StartsWith(NativeMethods.Win32LongPathPrefix, StringComparison.Ordinal))
            target = target[4..];

        if (!Path.IsPathRooted(target))
        {
            var baseDir = Path.GetDirectoryName(linkPath) ?? string.Empty;
            target = Path.GetFullPath(Path.Combine(baseDir, target));
        }

        return target.TrimEnd('\\');
    }

    /// <summary>
    /// 删除一个链接本身。目录链接用 <c>Directory.Delete(recursive: false)</c> 只会移除链接，
    /// 不会递归删除目标内容 —— 这正是我们要的语义。
    /// </summary>
    public static void DeleteLink(string linkPath)
    {
        var kind = GetLinkKind(linkPath);
        if (kind == LinkKind.None)
            throw new InvalidOperationException($"路径不是链接，拒绝按链接方式删除: {linkPath}");

        Directory.Delete(linkPath, recursive: false);
    }

    /// <summary>判断一个目录是否为空（不含任何文件或子目录）。</summary>
    public static bool IsDirectoryEmpty(string path)
    {
        if (!Directory.Exists(path)) return true;
        using var e = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
        return !e.MoveNext();
    }
}
