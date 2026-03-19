namespace sqlwebapi;
using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Text.Json;

public static class JsonHelpers
{
    public static bool TryParseJsonElement(string? input, out JsonElement element, out string? error)
    {
        element = default;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Input is null or whitespace.";
            return false;
        }

        // Trim leading BOM if present
        input = TrimUtf8Bom(input);

        // Peek first non-whitespace char
        var first = FirstNonWhitespace(input);
        if (first == '\0')
        {
            error = "Input contains only whitespace.";
            return false;
        }

        try
        {
            // Direct attempt
            element = JsonSerializer.Deserialize<JsonElement>(input);
            return true;
        }
        catch (JsonException)
        {
            // If it *looks like* a JSON string (double-encoded JSON), try unwrapping once
            if (first == '"')
            {
                try
                {
                    var inner = JsonSerializer.Deserialize<string>(input);
                    if (!string.IsNullOrWhiteSpace(inner))
                    {
                        element = JsonSerializer.Deserialize<JsonElement>(inner);
                        return true;
                    }
                }
                catch { /* ignore and fall through */ }
            }

            // Last resort: clearer diagnostics
            var prefix = input.Length > 120 ? input[..120] + "..." : input;
            error = "Failed to parse JSON. Starts with: " + prefix;
            return false;
        }
    }

    public static bool TryParseJsonElement(Stream stream, out JsonElement element, out string? error)
    {
        element = default;
        error = null;

        if (!stream.CanSeek)
        {
            // If this is ASP.NET Core Request.Body, call HttpRequest.EnableBuffering() first
            // then stream.Position = 0
        }

        if (stream.CanSeek) stream.Position = 0;

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = reader.ReadToEnd();

        // Reset for callers who will read again
        if (stream.CanSeek) stream.Position = 0;

        return TryParseJsonElement(text, out element, out error);
    }

    private static char FirstNonWhitespace(string s)
    {
        for (int i = 0; i < s.Length; i++)
            if (!char.IsWhiteSpace(s[i])) return s[i];
        return '\0';
    }

    private static string TrimUtf8Bom(string s)
    {
        // UTF-8 BOM: 0xEF 0xBB 0xBF
        if (s.Length >= 1 && s[0] == '\uFEFF') return s.Substring(1);
        return s;
    }
}
