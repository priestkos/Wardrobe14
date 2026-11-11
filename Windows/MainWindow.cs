using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Wardrobe14.Ipc;
using Wardrobe14.Logic;

namespace Wardrobe14.Windows;

/// <summary> M1: read-only grid of every mod against the player's collection plus pinned collections. </summary>
public sealed class MainWindow : Window
{
    private readonly PenumbraIpc penumbra;
    private readonly GlamourerIpc glamourer;
    private readonly Configuration config;
    private readonly Action saveConfig;

    private List<ModRow> mods = [];
    private List<string> folders = [];
    private Dictionary<Guid, string> collectionNames = [];
    private readonly List<(Guid Id, string Label, IReadOnlyDictionary<string, ModCell> Cells)> columns = [];
    private List<IReadOnlyDictionary<string, ModCell>> cells = [];
    private List<ModRow> rows = [];
    private (string, string, int, bool)? rowsKey; // filter inputs the cached rows were built from; null = rebuild

    private string text = string.Empty;
    private string folder = string.Empty;
    private int onInColumn = -1;
    private bool differsOnly;

    public MainWindow(PenumbraIpc penumbra, GlamourerIpc glamourer, Configuration config, Action saveConfig)
        : base("Wardrobe14###Wardrobe14Main")
    {
        this.penumbra = penumbra;
        this.glamourer = glamourer;
        this.config = config;
        this.saveConfig = saveConfig;
        Size = new Vector2(760, 520);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(420, 240), MaximumSize = new Vector2(4000, 4000) };
    }

    public override void OnOpen()
        => penumbra.SettingsStale = true; // the player's collection can change without any Penumbra event (zone, login)

    public override void Draw()
    {
        DrawStatus();
        if (!penumbra.Ok)
            return;

        Reload();
        DrawColumnPicker();
        DrawFilters();
        DrawGrid();
    }

    private void DrawStatus()
    {
        Status("Penumbra", penumbra.Problem, $"{penumbra.Version.Breaking}.{penumbra.Version.Feature}");
        ImGui.SameLine();
        Status("Glamourer", glamourer.Problem, $"{glamourer.Version.Major}.{glamourer.Version.Minor}");
        ImGui.SameLine();
        if (ImGui.SmallButton("Recheck"))
        {
            penumbra.Check();
            glamourer.Check();
            penumbra.ModsStale = penumbra.SettingsStale = true;
        }

        if (penumbra.Ok)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Redraw me"))
                penumbra.RedrawPlayer();
        }
    }

    private static void Status(string name, string? problem, string version)
    {
        if (problem == null)
            ImGui.TextUnformatted($"{name}: OK (API {version})");
        else
            ImGui.TextColored(new Vector4(1f, 0.45f, 0.45f, 1f), $"{name}: {problem}");
    }

    private void Reload()
    {
        if (penumbra.ModsStale)
        {
            penumbra.ModsStale = false; // clear first, so an event during the load marks it again
            mods = penumbra.Mods();
            folders = ModGrid.Folders(mods);
            rowsKey = null;
        }

        if (!penumbra.SettingsStale)
            return;
        penumbra.SettingsStale = false;
        collectionNames = penumbra.Collections();
        columns.Clear();
        var player = penumbra.PlayerCollection();
        if (player is { } p)
            columns.Add((p.Id, $"{p.Name} (you)", penumbra.Settings(p.Id)));
        foreach (var id in config.PinnedCollections)
            if (columns.All(c => c.Id != id) && collectionNames.TryGetValue(id, out var name))
                columns.Add((id, name, penumbra.Settings(id)));
        if (onInColumn >= columns.Count)
            onInColumn = -1;
        cells = columns.Select(c => c.Cells).ToList();
        rowsKey = null;
    }

    private void DrawColumnPicker()
    {
        ImGui.TextUnformatted("Columns:");
        foreach (var id in config.PinnedCollections.ToList())
        {
            ImGui.SameLine();
            var name = collectionNames.GetValueOrDefault(id, "(deleted collection)");
            if (ImGui.SmallButton($"{name}  x##unpin{id}"))
            {
                config.PinnedCollections.Remove(id);
                saveConfig();
                penumbra.SettingsStale = true;
            }
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(200 * Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale);
        using var combo = Dalamud.Interface.Utility.Raii.ImRaii.Combo("##addcol", "+ Add collection");
        if (!combo)
            return;
        foreach (var (id, name) in collectionNames.OrderBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase))
        {
            if (columns.Any(c => c.Id == id) || !ImGui.Selectable($"{name}##{id}"))
                continue;
            config.PinnedCollections.Add(id);
            saveConfig();
            penumbra.SettingsStale = true;
        }
    }

    private void DrawFilters()
    {
        var scale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
        ImGui.SetNextItemWidth(180 * scale);
        ImGui.InputTextWithHint("##text", "Search name or path", ref text, 256);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(180 * scale);
        using (var combo = Dalamud.Interface.Utility.Raii.ImRaii.Combo("##folder", folder.Length == 0 ? "All folders" : folder))
        {
            if (combo)
            {
                if (ImGui.Selectable("All folders", folder.Length == 0))
                    folder = string.Empty;
                foreach (var f in folders)
                    if (ImGui.Selectable(f, f == folder))
                        folder = f;
            }
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(160 * scale);
        var onLabel = onInColumn < 0 ? "Any state" : $"On in {columns[onInColumn].Label}";
        using (var combo = Dalamud.Interface.Utility.Raii.ImRaii.Combo("##onin", onLabel))
        {
            if (combo)
            {
                if (ImGui.Selectable("Any state", onInColumn < 0))
                    onInColumn = -1;
                for (var i = 0; i < columns.Count; i++)
                    if (ImGui.Selectable($"On in {columns[i].Label}##on{i}", onInColumn == i))
                        onInColumn = i;
            }
        }

        ImGui.SameLine();
        ImGui.Checkbox("Differs between columns", ref differsOnly);
    }

    private void DrawGrid()
    {
        // 888+ mods: filter only when an input changed, not every frame.
        var key = (text, folder, onInColumn, differsOnly);
        if (rowsKey != key)
        {
            rows = ModGrid.Filter(mods, cells, text, folder, onInColumn, differsOnly);
            rowsKey = key;
        }

        ImGui.TextDisabled($"{rows.Count} of {mods.Count} mods");

        const ImGuiTableFlags flags = ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV
                                    | ImGuiTableFlags.Resizable;
        if (!ImGui.BeginTable("##grid", 2 + columns.Count, flags))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Mod", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Folder", ImGuiTableColumnFlags.WidthStretch);
        foreach (var c in columns)
            ImGui.TableSetupColumn(c.Label, ImGuiTableColumnFlags.WidthFixed, 110 * Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale);
        ImGui.TableHeadersRow();

        var clipper = ImGui.ImGuiListClipper();
        clipper.Begin(rows.Count);
        while (clipper.Step())
        {
            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                var row = rows[i];
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.Name);
                ImGui.TableNextColumn();
                var slash = row.Path.LastIndexOf('/');
                ImGui.TextDisabled(slash > 0 ? row.Path[..slash] : string.Empty);
                foreach (var cell in cells)
                {
                    ImGui.TableNextColumn();
                    DrawCell(cell, row.Directory);
                }
            }
        }

        clipper.End();
        clipper.Destroy();
        ImGui.EndTable();
    }

    private static void DrawCell(IReadOnlyDictionary<string, ModCell> column, string directory)
    {
        if (!column.TryGetValue(directory, out var cell))
        {
            ImGui.TextDisabled("off");
            return;
        }

        var label = (cell.Enabled ? "on" : "off") + (cell.Inherited ? " (inh)" : string.Empty) + (cell.Temporary ? " (tmp)" : string.Empty);
        if (cell.Enabled)
            ImGui.TextUnformatted(label);
        else
            ImGui.TextDisabled(label);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Priority {cell.Priority}{(cell.Inherited ? "\nInherited from a parent collection" : string.Empty)}{(cell.Temporary ? "\nTemporary setting" : string.Empty)}");
    }
}
