using System.Text;

namespace MillionHoursTimer.Core.Csv;

/// <summary>
/// RFC 4180 形式の CSV を読み書きする。
/// Excel やテキストエディタで編集されたファイルも読めるよう、読み込みは寛容にしている。
/// </summary>
public static class CsvFormat
{
    public static List<string[]> Parse(string text)
    {
        var rows = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var atFieldStart = true;

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
            atFieldStart = true;
        }

        void EndRow()
        {
            EndField();
            // 完全な空行は無視する
            if (!(fields.Count == 1 && fields[0].Length == 0)) rows.Add(fields.ToArray());
            fields.Clear();
        }

        var i = text.Length > 0 && text[0] == '﻿' ? 1 : 0;
        for (; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case '"' when atFieldStart:
                    inQuotes = true;
                    atFieldStart = false;
                    break;
                case ',':
                    EndField();
                    break;
                case '\r':
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    EndRow();
                    break;
                case '\n':
                    EndRow();
                    break;
                default:
                    field.Append(c);
                    atFieldStart = false;
                    break;
            }
        }

        if (field.Length > 0 || fields.Count > 0 || !atFieldStart) EndRow();
        return rows;
    }

    public static string FormatField(string? value)
    {
        value ??= "";
        var needsQuote = value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            || (value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1])));
        return needsQuote ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    public static string FormatRow(IEnumerable<string?> fields) => string.Join(",", fields.Select(FormatField));
}
