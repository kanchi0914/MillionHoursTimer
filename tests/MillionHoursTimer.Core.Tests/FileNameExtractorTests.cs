using MillionHoursTimer.Core.Tracking;

namespace MillionHoursTimer.Core.Tests;

public class FileNameExtractorTests
{
    private static readonly string[] Images = [".gif", ".jpg", ".png", ".psd", ".sai"];

    [Theory]
    [InlineData("illust.psd @ 66.7% (RGB/8)", "illust.psd")]
    [InlineData("SAI - [rough.sai]", "[rough.sai")]
    [InlineData("[*]image.png-1.0 (RGB color 8-bit gamma integer, GIMP built-in sRGB, 1 layer) 800x600 – GIMP", "[*]image.png")]
    [InlineData("ILLUST.PNG - Viewer", "ILLUST.PNG")]
    public void ExtractsByExtension(string title, string expected) =>
        Assert.Equal(expected, FileNameExtractor.Extract(title, Images, splitBySpace: true, useHyphen: false));

    [Fact]
    public void SplitBySpaceReturnsTheTokenContainingTheExtension()
    {
        const string title = "ページ管理 - 表紙 ラフ.png";
        Assert.Equal("ラフ.png", FileNameExtractor.Extract(title, Images, splitBySpace: true, useHyphen: false));
        Assert.Equal("ページ管理 - 表紙 ラフ.png", FileNameExtractor.Extract(title, Images, splitBySpace: false, useHyphen: false));
    }

    [Theory]
    [InlineData("TimeCounter.cs - MillionHoursTimer - Visual Studio Code", true, "TimeCounter.cs")]
    [InlineData("my notes - Notepad", false, "my notes")]
    [InlineData("my notes - Notepad", true, "my")]
    [InlineData("README.md — Atom", true, "README.md")]
    public void FallsBackToHyphenSeparatedTitles(string title, bool split, string expected) =>
        Assert.Equal(expected, FileNameExtractor.Extract(title, [], split, useHyphen: true));

    [Theory]
    [InlineData("")]
    [InlineData("No separator here")]
    public void ReturnsNullWhenNothingMatches(string title) =>
        Assert.Null(FileNameExtractor.Extract(title, Images, splitBySpace: true, useHyphen: true));

    [Fact]
    public void ExtensionMatchTakesPriorityOverHyphen() =>
        Assert.Equal("a.png", FileNameExtractor.Extract("a.png - Viewer", Images, true, true));
}
