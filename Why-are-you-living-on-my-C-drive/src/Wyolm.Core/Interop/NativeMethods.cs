using System.Runtime.InteropServices;

namespace Wyolm.Core.Interop;

/// <summary>
/// 直接封装创建目录链接所需的 Win32 API。
/// 这里刻意不使用 <c>mklink</c> 命令行：进程内 P/Invoke 能拿到真实 NTSTATUS/Win32 错误码，
/// 也避免了命令行转义、Unicode 路径和空格路径带来的坑。
/// </summary>
internal static partial class NativeMethods
{
    // ---- 重解析点（Reparse Point）相关 ----
    internal const uint IO_REPARSE_TAG_MOUNT_POINT = 0xA0000003; // 目录联接（Junction）
    internal const uint IO_REPARSE_TAG_SYMLINK = 0xA000000C;     // 符号链接（Symlink）
    internal const uint FSCTL_SET_REPARSE_POINT = 0x000900A4;
    internal const uint FSCTL_GET_REPARSE_POINT = 0x000900A8;

    internal const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;
    internal const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    internal const uint GENERIC_WRITE = 0x40000000;
    internal const uint GENERIC_READ = 0x80000000;
    internal const uint FILE_SHARE_READ = 0x00000001;
    internal const uint FILE_SHARE_WRITE = 0x00000002;
    internal const uint FILE_SHARE_DELETE = 0x00000004;
    internal const uint OPEN_EXISTING = 3;
    internal const uint INVALID_FILE_ATTRIBUTES = 0xFFFFFFFF;

    internal const uint SYMBOLIC_LINK_FLAG_DIRECTORY = 0x1;
    internal const uint SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE = 0x2;

    /// <summary>目录联接的替代名必须以 NT 对象管理器前缀开头。</summary>
    internal const string NtPathPrefix = @"\??\";
    internal const string Win32LongPathPrefix = @"\\?\";
    internal const string Win32LongUncPrefix = @"\\?\UNC\";

    [LibraryImport("kernel32.dll", EntryPoint = "CreateSymbolicLinkW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, uint dwFlags);

    [LibraryImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeviceIoControl(
        SafeFileHandleWrapper hDevice,
        uint dwIoControlCode,
        byte[]? lpInBuffer,
        int nInBufferSize,
        byte[]? lpOutBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        IntPtr lpOverlapped);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial SafeFileHandleWrapper CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [LibraryImport("kernel32.dll", EntryPoint = "GetFileAttributesW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint GetFileAttributes(string lpFileName);

    [LibraryImport("kernel32.dll", EntryPoint = "GetVolumePathNameW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetVolumePathName(string lpszFileName, [Out] char[] lpszVolumePathName, int cchBufferLength);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WIN32_FIND_DATA
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0; // 重解析点标签
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    // WIN32_FIND_DATA 含 ByValTStr 字段，源生成器不支持，这里保留传统 DllImport。
    [DllImport("kernel32.dll", EntryPoint = "FindFirstFileW", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern SafeFindHandleWrapper FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

    [LibraryImport("kernel32.dll", EntryPoint = "FindClose", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FindClose(IntPtr hFindFile);

    /// <summary>
    /// 把普通路径转换为 <c>\\?\</c> 长路径形式，绕过 MAX_PATH 限制。
    /// </summary>
    internal static string ToLongPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        if (path.StartsWith(Win32LongPathPrefix, StringComparison.Ordinal)) return path;
        if (path.StartsWith(@"\\", StringComparison.Ordinal))
            return Win32LongUncPrefix + path[2..];
        return Win32LongPathPrefix + path;
    }

    /// <summary>
    /// 把 Win32 路径转换为 NT 对象管理器路径（<c>\??\C:\...</c>），Junction 的替代名必须用它。
    /// </summary>
    internal static string ToNtPath(string path)
    {
        var p = path;
        if (p.StartsWith(Win32LongPathPrefix, StringComparison.Ordinal))
            p = p[4..];
        if (p.StartsWith(Win32LongUncPrefix, StringComparison.Ordinal))
            return @"\??\UNC\" + p[8..];
        if (p.StartsWith(@"\\", StringComparison.Ordinal))
            return @"\??\UNC\" + p[2..];
        return NtPathPrefix + p;
    }
}

/// <summary>
/// 极简的 SafeHandle 包装，保证句柄一定被释放。
/// </summary>
internal sealed class SafeFileHandleWrapper : SafeHandle
{
    public SafeFileHandleWrapper() : base(IntPtr.Zero, ownsHandle: true) { }
    public override bool IsInvalid => handle == IntPtr.Zero || handle == new IntPtr(-1);
    protected override bool ReleaseHandle() => CloseHandle(handle);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}

internal sealed class SafeFindHandleWrapper : SafeHandle
{
    public SafeFindHandleWrapper() : base(new IntPtr(-1), ownsHandle: true) { }
    public override bool IsInvalid => handle == new IntPtr(-1) || handle == IntPtr.Zero;
    protected override bool ReleaseHandle() => NativeMethods.FindClose(handle);
}
