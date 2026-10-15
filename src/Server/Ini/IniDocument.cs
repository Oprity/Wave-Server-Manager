// AC Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 AC Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;

namespace AcServerManager.Ini;

/// <summary>
/// Round-trip-safe INI reader/writer for Assetto Corsa <c>server_cfg.ini</c> and
/// <c>entry_list.ini</c>. Unknown keys, section order, comments and blank lines
/// are preserved: parsing then serializing without edits returns byte-identical text
/// (modulo newline normalization to <c>\n</c>).
/// </summary>
public sealed class IniDocument
{
    private readonly List<IniLine> _lines = new();
    private readonly Dictionary<(string Section, string Key), int> _index;

    public IniDocument()
    {
        _index = new Dictionary<(string Section, string Key), int>(SectionKeyComparer.Instance);
    }

    public IReadOnlyList<IniLine> Lines => _lines;

    public static IniDocument Parse(string text)
    {
        var doc = new IniDocument();
        var currentSection = string.Empty;

        // Normalize newlines; remember original had trailing newline via sentinel blank handling.
        var normalized = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        var rawLines = normalized.Split('\n');

        // A trailing "\n" produces a final empty element — that represents the file's
        // trailing newline, not a blank line, so drop exactly one.
        var endsWithNewline = rawLines.Length > 0 && rawLines[^1] == string.Empty;
        var count = endsWithNewline ? rawLines.Length - 1 : rawLines.Length;

        for (var i = 0; i < count; i++)
        {
            var raw = rawLines[i];
            var trimmed = raw.Trim();

            if (trimmed.Length == 0)
            {
                doc._lines.Add(new BlankLine(raw));
                continue;
            }

            if (trimmed.StartsWith(';') || trimmed.StartsWith('#'))
            {
                doc._lines.Add(new CommentLine(raw));
                continue;
            }

            if (trimmed.StartsWith('['))
            {
                var end = trimmed.IndexOf(']');
                if (end > 1)
                {
                    var name = trimmed.Substring(1, end - 1).Trim();
                    var trail = trimmed.Substring(end + 1); // e.g. " ; comment" or ""
                    currentSection = name;
                    doc._lines.Add(new SectionLine(raw, name, trail));
                    continue;
                }
                // Malformed bracket line: keep as comment to avoid data loss.
                doc._lines.Add(new CommentLine(raw));
                continue;
            }

            var eq = raw.IndexOf('=');
            if (eq < 0)
            {
                // No '=', not a section, not a comment: preserve verbatim as comment-like line.
                doc._lines.Add(new CommentLine(raw));
                continue;
            }

            var key = raw.Substring(0, eq).Trim();
            var valueAndComment = raw.Substring(eq + 1);
            var (value, trailing) = SplitTrailingComment(valueAndComment);
            var line = new KeyLine(raw, currentSection, key, value.Trim(), trailing);
            var idx = doc._lines.Count;
            doc._lines.Add(line);
            // First occurrence wins for lookup; duplicates are preserved in output.
            doc._index.TryAdd((currentSection, key), idx);
        }

        return doc;
    }

    /// <summary>
    /// Split "value ; comment" into value + trailing comment.
    /// Only treats ';' or '#' as a comment marker when preceded by whitespace,
    /// so passwords like "p;ss" survive.
    /// </summary>
    internal static (string Value, string Trailing) SplitTrailingComment(string rest)
    {
        for (var i = 0; i < rest.Length; i++)
        {
            var c = rest[i];
            if ((c == ';' || c == '#') && i > 0 && char.IsWhiteSpace(rest[i - 1]))
            {
                return (rest.Substring(0, i), rest.Substring(i - 1));
            }
        }
        return (rest, string.Empty);
    }

    public string? GetValue(string section, string key)
    {
        if (_index.TryGetValue((section ?? string.Empty, key), out var idx) &&
            _lines[idx] is KeyLine kl)
        {
            return kl.Value;
        }
        return null;
    }

    public void SetValue(string section, string key, string value)
    {
        section ??= string.Empty;
        value ??= string.Empty;

        if (_index.TryGetValue((section, key), out var idx) && _lines[idx] is KeyLine existing)
        {
            _lines[idx] = existing with { Value = value, IsModified = true };
            return;
        }

        // Append: ensure section exists, then add key at end of that section.
        var sectionIdx = -1;
        for (var i = _lines.Count - 1; i >= 0; i--)
        {
            if (_lines[i] is SectionLine s &&
                string.Equals(s.Name, section, StringComparison.OrdinalIgnoreCase))
            {
                sectionIdx = i;
                break;
            }
        }

        if (sectionIdx < 0 && section.Length > 0)
        {
            if (_lines.Count > 0 && _lines[^1] is not BlankLine)
            {
                _lines.Add(new BlankLine(string.Empty));
            }
            _lines.Add(new SectionLine($"[{section}]", section, string.Empty));
            sectionIdx = _lines.Count - 1;
        }

        var insertAt = _lines.Count;
        if (sectionIdx >= 0)
        {
            insertAt = sectionIdx + 1;
            while (insertAt < _lines.Count && _lines[insertAt] is not SectionLine)
            {
                insertAt++;
            }
        }

        var newLine = new KeyLine($"{key}={value}", section, key, value, string.Empty)
        {
            IsModified = true,
        };
        _lines.Insert(insertAt, newLine);
        RebuildIndex();
    }

    public bool HasSection(string section) =>
        _lines.OfType<SectionLine>().Any(s => string.Equals(s.Name, section, StringComparison.OrdinalIgnoreCase));

    public override string ToString() => Serialize();

    public string Serialize()
    {
        var sb = new StringBuilder();
        foreach (var line in _lines)
        {
            sb.Append(line switch
            {
                BlankLine b => b.Raw,
                CommentLine c => c.Raw,
                SectionLine s => s.Raw,
                KeyLine k => k.IsModified ? $"{k.Key}={k.Value}{k.Trailing}" : k.Raw,
                _ => string.Empty,
            });
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private void RebuildIndex()
    {
        _index.Clear();
        for (var i = 0; i < _lines.Count; i++)
        {
            if (_lines[i] is KeyLine k)
            {
                _index.TryAdd((k.Section, k.Key), i);
            }
        }
    }

    private sealed class SectionKeyComparer : IEqualityComparer<(string Section, string Key)>
    {
        public static readonly SectionKeyComparer Instance = new();
        public bool Equals((string Section, string Key) x, (string Section, string Key) y) =>
            string.Equals(x.Section, y.Section, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Key, y.Key, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((string Section, string Key) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Section ?? string.Empty),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Key ?? string.Empty));
    }
}

public abstract record IniLine;

public sealed record BlankLine(string Raw) : IniLine;

public sealed record CommentLine(string Raw) : IniLine;

public sealed record SectionLine(string Raw, string Name, string Trailing) : IniLine;

/// <summary>A KEY=VALUE line. <c>Raw</c> is the original text; after <c>SetValue</c> the
/// line is re-emitted as <c>KEY=VALUE + Trailing</c>.</summary>
public sealed record KeyLine(string Raw, string Section, string Key, string Value, string Trailing) : IniLine
{
    public bool IsModified { get; set; }
}
