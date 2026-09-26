using System.Text.RegularExpressions;

namespace Lanw.Core.Tests;

// 校验 RandomNameUtil（web/public/random.name.json + generateRandomGameName 移植）的行为。
public partial class RandomNameUtilTests
{
    // 生成名 = 中文组合（前缀"…的" + 后缀）+ 可选随机数字；长度恒为 7~9。
    [GeneratedRegex(@"^[\p{IsCJKUnifiedIdeographs}]+[0-9]*$")]
    private static partial Regex NamePattern();

    [Fact]
    public void Generate_LengthAlwaysWithin7To9()
    {
        for (var i = 0; i < 500; i++)
        {
            var name = Lanw.Core.Utils.RandomNameUtil.Generate();
            Assert.InRange(name.Length, 7, 9);
        }
    }

    [Fact]
    public void Generate_MatchesChinesePlusOptionalDigitsPattern()
    {
        for (var i = 0; i < 500; i++)
        {
            var name = Lanw.Core.Utils.RandomNameUtil.Generate();
            Assert.Matches(NamePattern(), name);
        }
    }

    [Fact]
    public void Generate_PrefixEndsWithDe_AndBodyIsSixChars()
    {
        // 词库前缀均为 3 字（"…的"）+ 后缀 3 字，正文为 6 个中文字，第 3 字必为"的"。
        for (var i = 0; i < 200; i++)
        {
            var name = Lanw.Core.Utils.RandomNameUtil.Generate();
            Assert.True(char.IsLetter(name[0]), $"首字符应为中文：{name}");
            Assert.Equal('的', name[2]);
            Assert.Equal(6, name.Count(char.IsLetter));
        }
    }

    [Fact]
    public void Generate_IsRandomEnough_AcrossManySamples()
    {
        var names = new HashSet<string>();
        for (var i = 0; i < 200; i++)
        {
            names.Add(Lanw.Core.Utils.RandomNameUtil.Generate());
        }

        // 185 前缀 × 261 后缀 × 数字后缀组合空间巨大，200 次采样不重复值应远超一半。
        Assert.True(names.Count >= 100, $"随机性不足：200 次仅 {names.Count} 个不同值");
    }

    [Fact]
    public void Generate_DigitSuffixPadStartsCorrectly()
    {
        // 数字段若存在应占 1~3 位（正文 6 字 + 数字 => 7~9），且不含非数字杂质。
        for (var i = 0; i < 200; i++)
        {
            var name = Lanw.Core.Utils.RandomNameUtil.Generate();
            var digits = new string(name.Where(char.IsDigit).ToArray());
            Assert.InRange(digits.Length, 1, 3);
            Assert.Equal(name.Length, 6 + digits.Length);
        }
    }
}
