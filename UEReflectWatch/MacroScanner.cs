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

        public MacroEntry(MacroKind kind, int line, string raw)
        {
            Kind = kind;
            Line = line;
            Raw = raw;
        }

        // Identity key used for diffing. Kind + raw text so that specifier
        // changes (e.g. EditAnywhere -> EditDefaultsOnly) are caught.
        public string Key => $"{Kind}|{Raw}";
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
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var match = Pattern.Match(lines[i]);
                if (!match.Success) continue;

                if (!Enum.TryParse<MacroKind>(match.Groups[1].Value, out var kind)) continue;

                results.Add(new MacroEntry(kind, i + 1, lines[i].Trim()));
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