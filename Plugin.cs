using System.Diagnostics;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Wardrobe14.Ipc;
using Wardrobe14.Windows;

namespace Wardrobe14;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] private static IDalamudPluginInterface PluginInterface { get; set; } = null!;
    [PluginService] private static ICommandManager CommandManager { get; set; } = null!;
    [PluginService] private static IChatGui Chat { get; set; } = null!;
    [PluginService] private static IPluginLog Log { get; set; } = null!;

    private const string CommandName = "/pmm";
    private readonly Configuration config;
    private readonly PenumbraIpc penumbra;
    private readonly GlamourerIpc glamourer;
    private readonly WindowSystem windowSystem = new("Wardrobe14");
    private readonly MainWindow mainWindow;

    public Plugin()
    {
        config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        penumbra = new PenumbraIpc(PluginInterface);
        glamourer = new GlamourerIpc(PluginInterface);
        mainWindow = new MainWindow(penumbra, glamourer, config, () => PluginInterface.SavePluginConfig(config));
        windowSystem.AddWindow(mainWindow);

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += mainWindow.Toggle;
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Wardrobe14. /pmm probe = read-only timing report, /pmm redraw = redraw your character.",
        });
    }

    public void Dispose()
    {
        CommandManager.RemoveHandler(CommandName);
        PluginInterface.UiBuilder.OpenMainUi -= mainWindow.Toggle;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        windowSystem.RemoveAllWindows();
        glamourer.Dispose();
        penumbra.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "probe":
                Probe();
                break;
            case "redraw":
                penumbra.RedrawPlayer();
                break;
            default:
                mainWindow.Toggle();
                break;
        }
    }

    /// <summary> Read-only: versions, counts and how long the reads the grid depends on take at this install's size. </summary>
    private void Probe()
    {
        penumbra.Check();
        glamourer.Check();
        Print($"Penumbra: {penumbra.Problem ?? "OK"} (API {penumbra.Version.Breaking}.{penumbra.Version.Feature}); "
            + $"Glamourer: {glamourer.Problem ?? "OK"} (API {glamourer.Version.Major}.{glamourer.Version.Minor})");
        if (!penumbra.Ok)
            return;

        var sw = Stopwatch.StartNew();
        var mods = penumbra.Mods();
        var modsMs = sw.Elapsed.TotalMilliseconds;

        var collections = penumbra.Collections();
        var player = penumbra.PlayerCollection();
        double total = 0, max = 0;
        var entries = 0;
        foreach (var id in collections.Keys)
        {
            sw.Restart();
            entries += penumbra.Settings(id).Count;
            var ms = sw.Elapsed.TotalMilliseconds;
            total += ms;
            max = Math.Max(max, ms);
        }

        Print($"{mods.Count} mods listed with paths in {modsMs:F1} ms; {collections.Count} collections, "
            + $"settings read in {total:F1} ms total (slowest {max:F1} ms, {entries} mod entries); "
            + $"your collection: {player?.Name ?? "none"}. {penumbra.Problem ?? string.Empty}");
    }

    private static void Print(string message)
    {
        Chat.Print($"[Wardrobe14] {message}");
        Log.Information(message);
    }
}
