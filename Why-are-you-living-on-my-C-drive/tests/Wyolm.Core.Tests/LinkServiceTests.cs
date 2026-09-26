using Wyolm.Core.Interop;
using Wyolm.Core.Models;
using Wyolm.Core.Services;

namespace Wyolm.Core.Tests;

/// <summary>链接层：建、读、删，以及"删链接不能删数据"。</summary>
public sealed class LinkServiceTests
{
    [Fact]
    public void Junction_can_be_created_read_and_removed_without_harming_the_target()
    {
        using var sb = new TestSandbox();
        var target = sb.Dir(@"d\Target");
        sb.Write(@"d\Target\hello.txt", "world");

        var link = sb.Path_(@"c\Link");

        LinkService.Create(link, target, LinkKind.Junction);

        Assert.Equal(LinkKind.Junction, ReparsePoint.GetLinkKind(link));
        Assert.True(LinkService.PointsTo(link, target));

        // 透过链接应该能正常读写真实数据
        Assert.True(File.Exists(Path.Combine(link, "hello.txt")));
        Assert.Equal("world", File.ReadAllText(Path.Combine(link, "hello.txt")));
        File.WriteAllText(Path.Combine(link, "added-via-link.txt"), "hi");
        Assert.True(File.Exists(Path.Combine(target, "added-via-link.txt")));

        LinkService.RemoveLink(link);

        Assert.False(Directory.Exists(link));
        Assert.True(Directory.Exists(target));
        Assert.True(File.Exists(Path.Combine(target, "hello.txt")));
        Assert.True(File.Exists(Path.Combine(target, "added-via-link.txt")));
    }

    [Fact]
    public void Junction_replaces_an_empty_placeholder_directory_but_refuses_a_non_empty_one()
    {
        using var sb = new TestSandbox();
        var target = sb.Dir(@"d\Target");

        var empty = sb.Dir(@"c\EmptyPlaceholder");
        LinkService.Create(empty, target, LinkKind.Junction);
        Assert.Equal(LinkKind.Junction, ReparsePoint.GetLinkKind(empty));

        var busy = sb.Dir(@"c\BusyPlaceholder");
        sb.Write(@"c\BusyPlaceholder\keep.txt", "keep");

        Assert.ThrowsAny<Exception>(() => LinkService.Create(busy, target, LinkKind.Junction));
        Assert.True(File.Exists(Path.Combine(busy, "keep.txt")));
    }

    [Fact]
    public void Link_creation_fails_when_the_target_does_not_exist()
    {
        using var sb = new TestSandbox();
        var missing = sb.Path_(@"d\Missing");
        var link = sb.Path_(@"c\Link");

        Assert.ThrowsAny<Exception>(() => LinkService.Create(link, missing, LinkKind.Junction));
        Assert.False(Directory.Exists(link));
    }

    /// <summary>
    /// 这是整个工具里最关键的一条安全性质：
    /// 清理一个目录时，绝不能顺着里面的链接跑到别的盘上去。
    /// </summary>
    [Fact]
    public void Safe_delete_does_not_follow_nested_links()
    {
        using var sb = new TestSandbox();

        var precious = sb.Dir(@"d\PreciousData");
        sb.Write(@"d\PreciousData\important.txt", "绝对不能被删掉");

        var trash = sb.Dir(@"c\Trash");
        sb.Write(@"c\Trash\junk.txt", "随便删");
        Interop.ReparsePoint.CreateJunction(Path.Combine(trash, "shortcut"), precious);

        var ok = MigrationEngine.DeleteDirectorySafely(trash);

        Assert.True(ok);
        Assert.False(Directory.Exists(trash));
        Assert.True(Directory.Exists(precious));
        Assert.True(File.Exists(Path.Combine(precious, "important.txt")));
        Assert.Equal("绝对不能被删掉", File.ReadAllText(Path.Combine(precious, "important.txt")));
    }

    [Fact]
    public void Junction_creation_does_not_require_administrator_privileges()
    {
        // 目录联接的卖点就是免管理员；如果这条不再成立，说明引入了回归。
        using var sb = new TestSandbox();
        var target = sb.Dir(@"d\Target");
        var link = sb.Path_(@"c\Link");

        var ex = Record.Exception(() => LinkService.Create(link, target, LinkKind.Junction));

        Assert.Null(ex);
        Assert.Equal(LinkKind.Junction, ReparsePoint.GetLinkKind(link));
    }
}
