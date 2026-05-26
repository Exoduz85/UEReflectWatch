using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UEReflectWatch
{
    public enum MacroKind { UCLASS, UPROPERTY, UFUNCTION, USTRUCT, UENUM }

    public sealed class MacroEntry
    {
        public MacroKind Kind { get; }
        public int Line { get; }
        public string Raw { get; }
        // The declaration line immediately following the macro (variable type+name
        // or function signature). Included in the key so that renaming a variable
        // or changing its type is detected even if the macro specifiers are unchanged.
        public string DeclarationLine { get; }

        public MacroEntry(MacroKind kind, int line, string raw, string declarationLine = "")
        {
            Kind = kind;
            Line = line;
            Raw = raw;
            DeclarationLine = declarationLine;
        }

        // Identity key used for diffing. Kind + raw text + declaration so that
        // specifier changes AND variable type/name changes are all caught.
        public string Key => $"{Kind}|{Raw}|{DeclarationLine.Trim()}";
    }

    public sealed class MacroDiff
    {
        public List<MacroEntry> Added { get; } = new();
        public List<MacroEntry> Removed { get; } = new();
        public bool HasChanges => Added.Count > 0 || Removed.Count > 0;
    }

    public static class MacroScanner
    {
        private static readonly Regex Pattern = new Regex(
            @"^\s*(UCLASS|UPROPERTY|UFUNCTION|USTRUCT|UENUM)\s*(\(|$)",
            RegexOptions.Compiled | RegexOptions.Multiline
        );

        public static List<MacroEntry> Scan(string content)
        {
            var results = new List<MacroEntry>();
            // Normalise CRLF to LF so declarationLine trimming is consistent
            // regardless of whether content came from File.ReadAllText or an in-memory string.
            var lines = content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var match = Pattern.Match(lines[i]);
                if (!match.Success) continue;

                if (!Enum.TryParse<MacroKind>(match.Groups[1].Value, out var kind)) continue;

                // Capture the next non-blank line as the declaration.
                // This includes the variable type+name or function signature
                // so that renaming a variable or changing its type is detected.
                var declarationLine = "";
                for (int j = i + 1; j < lines.Length; j++)
                {
                    if (!string.IsNullOrWhiteSpace(lines[j]))
                    {
                        declarationLine = lines[j].Trim();
                        break;
                    }
                }

                results.Add(new MacroEntry(kind, i + 1, lines[i].Trim(), declarationLine));
            }

            return results;
        }

        public static MacroDiff Diff(List<MacroEntry> previous, List<MacroEntry> current)
        {
            var diff = new MacroDiff();

            var prevByKey = previous.GroupBy(e => e.Key)
                                    .ToDictionary(g => g.Key, g => g.Count());
            var currByKey = current.GroupBy(e => e.Key)
                                   .ToDictionary(g => g.Key, g => g.Count());

            // Entries present in current but not in previous (or with higher count) are added.
            foreach (var entry in current)
            {
                prevByKey.TryGetValue(entry.Key, out int prevCount);
                int currCount = currByKey[entry.Key];
                if (currCount > prevCount && !diff.Added.Any(a => a.Key == entry.Key))
                {
                    diff.Added.Add(entry);
                }
            }

            // Entries present in previous but not in current (or with lower count) are removed.
            foreach (var entry in previous)
            {
                int prevCount = prevByKey[entry.Key];
                currByKey.TryGetValue(entry.Key, out int currCount);
                if (prevCount > currCount && !diff.Removed.Any(r => r.Key == entry.Key))
                {
                    diff.Removed.Add(entry);
                }
            }

            return diff;
        }

        public static string SummariseDiff(MacroDiff diff, string fileName)
        {
            var parts = new List<string>();

            if (diff.Added.Count > 0)
            {
                var byKind = diff.Added.GroupBy(e => e.Kind)
                                       .Select(g => $"+{g.Count()} {g.Key}");
                parts.Add($"Added: {string.Join(", ", byKind)}");
            }

            if (diff.Removed.Count > 0)
            {
                var byKind = diff.Removed.GroupBy(e => e.Kind)
                                         .Select(g => $"-{g.Count()} {g.Key}");
                parts.Add($"Removed: {string.Join(", ", byKind)}");
            }

            return $"{fileName}: {string.Join(" | ", parts)}";
        }
    }
}