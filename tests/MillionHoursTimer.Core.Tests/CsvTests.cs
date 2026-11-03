using System.Text;
using MillionHoursTimer.Core.Csv;

namespace MillionHoursTimer.Core.Tests;

public class CsvTests
{
    [Fact]
    public void ParsesQuotedFieldsWithCommasQuotesAndNewlines()
    {
        var rows = CsvFormat.Parse("a,\"b,c\",\"say \"\"hi\"\"\"\r\n\"multi\nline\",x,\r\n");
        Assert.Equal(2, rows.Count);
        Assert.Equal(["a", "b,c", "say \"hi\""], rows[0]);
        Assert.Equal(["multi\nline", "x", ""], rows[1]);
    }

    [Fact]
    public void SkipsBlankLinesAndBomAndHandlesMissingTrailingNewline()
    {
        var rows = CsvFormat.Parse("﻿h1,h2\n\n1,2\n\r\n3,4");
        Assert.Equal([["h1", "h2"], ["1", "2"], ["3", "4"]], rows);
    }

    [Fact]
    public void FormatRoundTrips()
    {
        string[] fields = ["plain", "with,comma", "with \"quote\"", " leading space", "line\r\nbreak", "日本語.png", ""];
        var parsed = CsvFormat.Parse(CsvFormat.FormatRow(fields) + "\r\n");
        Assert.Equal(fields, Assert.Single(parsed));
    }

    [Fact]
    public void TableLooksUpColumnsByNameIgnoringOrderAndCase()
    {
        var table = CsvTable.Parse("Seconds,PROCESS\n10,code\n");
        Assert.Equal("code", table.Get(table.Rows[0], "process"));
        Assert.Equal("10", table.Get(table.Rows[0], "seconds"));
        Assert.Equal("", table.Get(table.Rows[0], "missing"));
    }

    [Fact]
    public void ReadsShiftJisWhenNotUtf8()
    {
        using var dir = new TempDir();
        var path = dir.Combine("sjis.csv");
        File.WriteAllBytes(path, Encoding.GetEncoding(932).GetBytes("名前,秒\nクリスタ,10\n"));

        var (text, isUtf8) = TextFile.Read(path);

        Assert.False(isUtf8);
        Assert.Contains("クリスタ", text);
    }

    [Fact]
    public void ReadsUtf8WithAndWithoutBom()
    {
        using var dir = new TempDir();
        var bom = dir.Combine("bom.csv");
        var noBom = dir.Combine("nobom.csv");
        File.WriteAllText(bom, "テスト", TextFile.Utf8Bom);
        File.WriteAllText(noBom, "テスト", TextFile.Utf8NoBom);

        Assert.Equal(("テスト", true), TextFile.Read(bom));
        Assert.Equal(("テスト", true), TextFile.Read(noBom));
    }
}

public class TimeTextTests
{
    [Theory]
    [InlineData("2026-10-29 21:03:12")]
    [InlineData("2026/10/29 21:03:12")]
    [InlineData("2026/10/29 21:03")]
    [InlineData("2026/1/5 9:03")]
    [InlineData("2026-10-29T21:03:12")]
    public void ParsesCommonDateFormats(string text)
    {
        Assert.True(TimeText.TryParse(text, out var value));
        Assert.Equal(2026, value.Year);
    }

    [Theory]
    [InlineData("3600", 3600)]
    [InlineData("1:00:00", 3600)]
    [InlineData("123:04:05", 123 * 3600 + 4 * 60 + 5)]
    [InlineData("12.6", 13)]
    public void ParsesSeconds(string text, long expected)
    {
        Assert.True(TimeText.TryParseSeconds(text, out var s));
        Assert.Equal(expected, s);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("1:99:00")]
    public void RejectsInvalidSeconds(string text) => Assert.False(TimeText.TryParseSeconds(text, out _));

    [Fact]
    public void FormatsDurationLikeOldVersion()
    {
        Assert.Equal("-", TimeText.FormatDuration(0));
        Assert.Equal("   1時間 2分 3秒", TimeText.FormatDuration(3723));
    }
}
