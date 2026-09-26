using System.Text;
using Wyolm.Core.Services;

namespace Wyolm.Core.Tests;

/// <summary>
/// 每个测试一套独立的临时目录 + 独立的 %LOCALAPPDATA% 替身，
/// 保证测试不会碰到真实的历史记录和日志，也不会互相干扰。
/// </summary>
public sealed class TestSandbox : IDisposable
{
    public string Root { get; }

    /// <summary>用来扮演"另一个盘"的目录。测试不跨物理卷，靠目录区分。</summary>
    public string TargetVolume { get; }

    public TestSandbox()
    {
        Root = Path.Combine(Path.GetTempPath(), "wyolm-tests", Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(Root);

        TargetVolume = Path.Combine(Root, "target-volume");
        Directory.CreateDirectory(TargetVolume);

        AppPaths.DataRoot = Path.Combine(Root, "_appdata");
        AppPaths.EnsureCreated();
    }

    /// <summary>创建（并按需递归创建）一个目录。</summary>
    public string Dir(string relative)
    {
        var path = Path.Combine(Root, relative);
        Directory.CreateDirectory(path);
        return path;
    }

    public string Path_(string relative) => Path.Combine(Root, relative);

    /// <summary>
    /// 写一个文本文件，自动创建上级目录。
    /// <para>刻意使用**不带 BOM** 的 UTF-8：<c>Encoding.UTF8</c> 会写入 3 字节 BOM，
    /// 而 <c>File.WriteAllText(path, text)</c> 不会。两者混用会让"文件大小"这类断言
    /// 平白多出 3 个字节的误差，掩盖真正的失败原因。真实目录里的文件也不该带 BOM。</para>
    /// </summary>
    public string Write(string relative, string content)
    {
        var path = Path.Combine(Root, relative);
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        File.WriteAllText(path, content, Utf8NoBom);
        return path;
    }

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
        catch (Exception)
        {
            // 临时目录删不掉不影响测试结论，交给系统清理。
        }
    }
}
