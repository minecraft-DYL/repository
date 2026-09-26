using System.ComponentModel;
using System.Security.Principal;
using Wyolm.Core.Interop;
using Wyolm.Core.Models;

namespace Wyolm.Core.Services;

/// <summary>链接的创建 / 识别 / 移除。</summary>
public static class LinkService
{
    private static bool? _cachedSymlinkCapability;
    private static string? _cachedSymlinkDetail;

    /// <summary>创建目录链接。默认用不需要管理员权限的目录联接。</summary>
    /// <param name="linkPath">链接应该出现的位置。父目录必须存在；该路径本身必须不存在或为空目录。</param>
    /// <param name="targetPath">链接指向的真实目录，必须已经存在。</param>
    public static void Create(string linkPath, string targetPath, LinkKind kind)
    {
        linkPath = PathGuard.Normalize(linkPath);
        targetPath = PathGuard.Normalize(targetPath);

        if (!Directory.Exists(targetPath))
            throw new DirectoryNotFoundException($"链接目标不存在：{targetPath}");

        if (ReparsePoint.GetLinkKind(targetPath) != LinkKind.None)
            throw new InvalidOperationException($"链接目标本身也是一个链接，请指向真实目录：{targetPath}");

        if (Directory.Exists(linkPath) || File.Exists(linkPath))
        {
            // 允许"空的占位目录"存在，先清掉它再建链接。
            if (Directory.Exists(linkPath) && ReparsePoint.IsDirectoryEmpty(linkPath))
                Directory.Delete(linkPath, recursive: false);
            else
                throw new IOException($"链接位置已被占用：{linkPath}");
        }

        var parent = Path.GetDirectoryName(linkPath);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

        switch (kind)
        {
            case LinkKind.Junction:
                ReparsePoint.CreateJunction(linkPath, targetPath);
                break;
            case LinkKind.SymbolicLink:
                ReparsePoint.CreateSymbolicLink(linkPath, targetPath);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "不支持的链接类型。");
        }

        // 立刻回读一次，确认链接真的生效并且指向正确的地方。
        var actualKind = ReparsePoint.GetLinkKind(linkPath);
        if (actualKind == LinkKind.None)
            throw new IOException($"链接创建后无法识别为重解析点：{linkPath}");

        var actualTarget = ReparsePoint.ReadLinkTarget(linkPath);
        if (actualTarget is not null &&
            !string.Equals(actualTarget.TrimEnd('\\'), targetPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            // 指向不对，拆掉重来，避免留下一个错误的链接。
            TryDeleteLink(linkPath);
            throw new IOException($"链接指向校验失败：期望 {targetPath}，实际 {actualTarget}");
        }
    }

    /// <summary>只删除链接本身，绝不递归删除目标内容。</summary>
    public static void RemoveLink(string linkPath)
    {
        linkPath = PathGuard.Normalize(linkPath);
        ReparsePoint.DeleteLink(linkPath);
    }

    /// <summary>删除链接，失败时返回 false 而不抛异常。</summary>
    public static bool TryDeleteLink(string linkPath)
    {
        try
        {
            if (ReparsePoint.GetLinkKind(linkPath) == LinkKind.None) return false;
            ReparsePoint.DeleteLink(linkPath);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>读取一个路径的链接信息。<c>(Kind, Target)</c>。</summary>
    public static (LinkKind Kind, string? Target) Inspect(string path)
    {
        var kind = ReparsePoint.GetLinkKind(path);
        return kind == LinkKind.None ? (LinkKind.None, null) : (kind, ReparsePoint.ReadLinkTarget(path));
    }

    /// <summary>该路径是否是指向指定目标的链接。</summary>
    public static bool PointsTo(string linkPath, string targetPath)
    {
        var (kind, target) = Inspect(linkPath);
        if (kind == LinkKind.None || target is null) return false;
        return string.Equals(target.TrimEnd('\\'), PathGuard.Normalize(targetPath).TrimEnd('\\'),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 探测本机能不能创建目录符号链接。
    /// <para>两个条件满足其一即可：进程有管理员权限，或者系统开启了开发者模式。</para>
    /// </summary>
    public static (bool Ok, string Detail) CheckSymbolicLinkCapability()
    {
        if (_cachedSymlinkCapability is bool cached)
            return (cached, _cachedSymlinkDetail ?? string.Empty);

        var (ok, detail) = ProbeSymbolicLink();
        _cachedSymlinkCapability = ok;
        _cachedSymlinkDetail = detail;
        return (ok, detail);
    }

    private static (bool, string) ProbeSymbolicLink()
    {
        var probeDir = Path.Combine(Path.GetTempPath(), "wyolm-symlink-probe-" + Guid.NewGuid().ToString("N")[..8]);
        var linkPath = Path.Combine(probeDir, "link");
        var targetPath = Path.Combine(probeDir, "target");

        try
        {
            Directory.CreateDirectory(targetPath);
            ReparsePoint.CreateSymbolicLink(linkPath, targetPath);
            var kind = ReparsePoint.GetLinkKind(linkPath);
            try { ReparsePoint.DeleteLink(linkPath); } catch (Exception) { }
            return kind == LinkKind.SymbolicLink
                ? (true, "本机可以创建符号链接。")
                : (false, "符号链接创建后未被识别为重解析点。");
        }
        catch (Win32Exception ex)
        {
            var admin = IsElevated();
            var hint = ex.NativeErrorCode == 1314
                ? (admin
                    ? "系统策略（SeCreateSymbolicLinkPrivilege）未授予当前账户。"
                    : "需要管理员权限，或在「设置 → 系统 → 开发者选项」里打开「开发人员模式」。")
                : ex.Message;
            return (false, $"当前无法创建目录符号链接（错误 {ex.NativeErrorCode}）。{hint} 建议改用目录联接。");
        }
        catch (Exception ex)
        {
            return (false, $"符号链接探测失败：{ex.Message}。建议改用目录联接。");
        }
        finally
        {
            try { if (Directory.Exists(probeDir)) Directory.Delete(probeDir, recursive: true); }
            catch (Exception) { }
        }
    }

    /// <summary>当前进程是否以管理员身份运行。</summary>
    public static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>开发者模式是否已开启。</summary>
    public static bool IsDeveloperModeEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock");
            return key?.GetValue("AllowDevelopmentWithoutDevLicense") is int v && v == 1;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 生成一段面向用户的链接能力说明，显示在界面上。
    /// </summary>
    public static string DescribeCapability()
    {
        var elevated = IsElevated();
        var devMode = IsDeveloperModeEnabled();
        var symlink = CheckSymbolicLinkCapability();

        var parts = new List<string>
        {
            "目录联接：可用（不需要管理员权限）",
            symlink.Ok ? "目录符号链接：可用" : "目录符号链接：不可用",
            elevated ? "当前以管理员身份运行" : "当前为普通权限",
            devMode ? "开发者模式：已开启" : "开发者模式：未开启",
        };

        return string.Join("　|　", parts);
    }
}
