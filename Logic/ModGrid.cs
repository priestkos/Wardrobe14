namespace Wardrobe14.Logic;

/// <summary> One mod's effective state in one collection. A mod missing from a collection's map has no settings there = off. </summary>
public readonly record struct ModCell(bool Enabled, int Priority, bool Inherited, bool Temporary);

/// <summary> A mod as Penumbra lists it: unique folder name, display name, sort path ("Hair/Bob"). </summary>
public sealed record ModRow(string Directory, string Name, string Path);

/// <summary> Pure filtering for the mod grid; no Dalamud types so the test project compiles it. </summary>
public static class ModGrid
{
    public static bool IsOn(IReadOnlyDictionary<string, ModCell> column, string directory)
        => column.TryGetValue(directory, out var cell) && cell.Enabled;

    /// <param name="text"> Case-insensitive match on name or path; empty = all. </param>
    /// <param name="folder"> Sort-path folder ("Hair" or "Hair/Short"); empty = all. </param>
    /// <param name="onInColumn"> Keep only mods on in this column index; -1 = no column filter. </param>
    /// <param name="differsOnly"> Keep only mods whose on/off differs between the columns. </param>
    public static List<ModRow> Filter(IReadOnlyList<ModRow> rows, IReadOnlyList<IReadOnlyDictionary<string, ModCell>> columns,
        string text, string folder, int onInColumn, bool differsOnly)
    {
        var result = new List<ModRow>();
        var prefix = folder.Trim().Trim('/');
        foreach (var row in rows)
        {
            if (text.Length > 0
             && !row.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
             && !row.Path.Contains(text, StringComparison.OrdinalIgnoreCase))
                continue;
            if (prefix.Length > 0 && !row.Path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
                continue;
            if (onInColumn >= 0 && onInColumn < columns.Count && !IsOn(columns[onInColumn], row.Directory))
                continue;
            if (differsOnly && columns.Select(c => IsOn(c, row.Directory)).Distinct().Count() < 2)
                continue;
            result.Add(row);
        }

        return result;
    }

    /// <summary> Every folder that holds at least one mod, including parents ("A", "A/B"), sorted. </summary>
    public static List<string> Folders(IEnumerable<ModRow> rows)
    {
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
            for (var i = row.Path.IndexOf('/'); i > 0; i = row.Path.IndexOf('/', i + 1))
                set.Add(row.Path[..i]);
        return [.. set];
    }
}
