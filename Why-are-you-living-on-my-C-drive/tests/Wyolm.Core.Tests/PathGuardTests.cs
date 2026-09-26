using Wyolm.Core.Models;
using Wyolm.Core.Services;

namespace Wyolm.Core.Tests;

/// <summary>路径守卫的行为。这一层是"不要把系统搞坏"的第一道闸门，必须有测试兜住。</summary>
public sealed class PathGuardTests
{
    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"C:\Windows")]
    [InlineData(@"C:\Program Files")]
    [InlineData(@"C:\Program Files (x86)")]
    [InlineData(@"C:\ProgramData")]
    [InlineData(@"C:\Users")]
    public void System_critical_paths_are_rejected(string source)
    {
        var report = PathGuard.Check(new MigrationRequest
        {
            SourcePath = source,
            DestinationPath = @"D:\somewhere",
            Mode = MigrationMode.MoveAndLink,
        });

        Assert.False(report.CanProceed);
        Assert.NotEmpty(report.Blockers);
    }

    [Theory]
    [InlineData(@"C:\Users\someone\AppData")]
    [InlineData(@"C:\Users\someone\AppData\Local")]
    [InlineData(@"C:\Users\someone\AppData\Roaming")]
    public void AppData_roots_are_rejected(string source)
    {
        var report = PathGuard.Check(new MigrationRequest
        {
            SourcePath = source,
            DestinationPath = @"D:\somewhere",
            Mode = MigrationMode.MoveAndLink,
        });

        Assert.False(report.CanProceed);
    }

    [Fact]
    public void Normal_user_folder_passes_preflight()
    {
        using var sb = new TestSandbox();
        var source = sb.Dir(@"c\SomeCache");
        sb.Write(@"c\SomeCache\a.bin", new string('x', 512));

        var report = PathGuard.Check(new MigrationRequest
        {
            SourcePath = source,
            DestinationPath = Path.Combine(sb.TargetVolume, "SomeCache"),
            Mode = MigrationMode.MoveAndLink,
        });

        Assert.True(report.CanProceed, string.Join("; ", report.Blockers.Select(b => $"{b.Name}={b.Detail}")));
    }

    [Fact]
    public void Destination_inside_source_is_rejected()
    {
        using var sb = new TestSandbox();
        var source = sb.Dir(@"c\Folder");

        var report = PathGuard.Check(new MigrationRequest
        {
            SourcePath = source,
            DestinationPath = Path.Combine(source, "inner"),
            Mode = MigrationMode.MoveAndLink,
        });

        Assert.False(report.CanProceed);
        Assert.Contains(report.Blockers, b => b.Name.Contains("目标不位于源之内"));
    }

    [Fact]
    public void Source_inside_destination_is_rejected()
    {
        using var sb = new TestSandbox();
        var destination = sb.Dir(@"d\Outer");
        var source = sb.Dir(@"d\Outer\inner");
        sb.Write(@"d\Outer\inner\a.txt", "x");

        var report = PathGuard.Check(new MigrationRequest
        {
            SourcePath = source,
            DestinationPath = destination,
            Mode = MigrationMode.MoveAndLink,
        });

        Assert.False(report.CanProceed);
        Assert.Contains(report.Blockers, b => b.Name.Contains("源不位于目标之内"));
    }

    [Fact]
    public void Non_empty_destination_is_rejected_without_merge_flag()
    {
        using var sb = new TestSandbox();
        var source = sb.Dir(@"c\Folder");
        sb.Write(@"c\Folder\a.txt", "a");
        var destination = sb.Dir(@"d\Folder");
        sb.Write(@"d\Folder\existing.txt", "existing");

        var blocked = PathGuard.Check(new MigrationRequest
        {
            SourcePath = source,
            DestinationPath = destination,
            Mode = MigrationMode.MoveAndLink,
        });

        Assert.False(blocked.CanProceed);
        Assert.Contains(blocked.Blockers, b => b.Name.Contains("目标未被占用"));

        var allowed = PathGuard.Check(new MigrationRequest
        {
            SourcePath = source,
            DestinationPath = destination,
            Mode = MigrationMode.MoveAndLink,
            AllowMergeIntoExisting = true,
        });

        Assert.True(allowed.CanProceed, string.Join("; ", allowed.Blockers.Select(b => $"{b.Name}={b.Detail}")));
    }

    [Fact]
    public void Link_mode_accepts_a_not_yet_existing_original_location()
    {
        using var sb = new TestSandbox();
        var real = sb.Dir(@"d\RealFolder");
        sb.Write(@"d\RealFolder\a.txt", "a");
        var linkPlace = sb.Path_(@"c\NotThereYet"); // 故意不创建

        var report = PathGuard.Check(new MigrationRequest
        {
            SourcePath = linkPlace,
            DestinationPath = real,
            Mode = MigrationMode.LinkOnly,
        });

        Assert.True(report.CanProceed, string.Join("; ", report.Blockers.Select(b => $"{b.Name}={b.Detail}")));
    }

    [Fact]
    public void Link_mode_rejects_an_occupied_original_location()
    {
        using var sb = new TestSandbox();
        var real = sb.Dir(@"d\RealFolder");
        sb.Write(@"d\RealFolder\a.txt", "a");
        var occupied = sb.Dir(@"c\Occupied");
        sb.Write(@"c\Occupied\user-data.txt", "keep me");

        var report = PathGuard.Check(new MigrationRequest
        {
            SourcePath = occupied,
            DestinationPath = real,
            Mode = MigrationMode.LinkOnly,
        });

        Assert.False(report.CanProceed);
    }

    [Fact]
    public void Moving_a_folder_that_is_already_a_link_is_rejected()
    {
        using var sb = new TestSandbox();
        var real = sb.Dir(@"d\Real");
        var link = sb.Path_(@"c\Link");
        Interop.ReparsePoint.CreateJunction(link, real);

        var report = PathGuard.Check(new MigrationRequest
        {
            SourcePath = link,
            DestinationPath = Path.Combine(sb.TargetVolume, "Link"),
            Mode = MigrationMode.MoveAndLink,
        });

        Assert.False(report.CanProceed);
        Assert.Contains(report.Blockers, b => b.Name == "源不是链接");
    }
}
